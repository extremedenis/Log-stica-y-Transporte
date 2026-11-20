using Microsoft.AspNetCore.DataProtection;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Logistica_y_transporte.Controllers;
using Logistica_y_transporte.Models;
using Logistica_y_transporte.Seguridad;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

internal sealed class AuditoriaRunner
{
    public static async Task Main(string[] args)
    {
        // No usa la conexión privada ni abre SQL Server. El proveedor real se sustituye antes de crear el host.
        var anterior=Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection","Server=127.0.0.1,1;Database=AuditUnused;Integrated Security=true;Connect Timeout=1");
        try
        {
            await using var factory=new HostAuditoria();
            if(args.Contains("--exportar-ui"))
            {
                var output=Environment.GetEnvironmentVariable("DENIS_UI_EVIDENCE_DIR")??throw new InvalidOperationException("Evidence directory required");
                Directory.CreateDirectory(output);using var fixtureClient=factory.Cliente();
                foreach(var language in new[]{"es","en"})foreach(var page in new[]{"blog","login"})
                {
                    var route=page=="blog"?"/Home/Blog":"/Identity/Account/Login";
                    var response=await fixtureClient.GetAsync(route+"?culture="+(language=="en"?"en-US":"es-GT")+"&ui-culture="+language);
                    if(!response.IsSuccessStatusCode)throw new Exception("UI export failed");
                    await File.WriteAllTextAsync(Path.Combine(output,page+"-"+language+".html"),await response.Content.ReadAsStringAsync());
                }
                Console.WriteLine("OK: 4 anonymous UI fixture pages exported; no SQL Server connection.");return;
            }
            using var anon=factory.Cliente();var services=factory.Services;var checks=0;
            void Check(bool ok,string message){if(!ok)throw new Exception("FALLA: "+message);checks++;}
            bool Denegado(HttpResponseMessage r)=>r.StatusCode==HttpStatusCode.Forbidden ||
                (r.StatusCode==HttpStatusCode.Redirect&&r.Headers.Location?.ToString().Contains("AccessDenied")==true);
            // Exercise the real registration/reset models rather than a copied regex.
            foreach(var model in new object[]{new Logistica_y_transporte.Areas.Identity.Pages.Account.RegisterModel.InputModel(),new Logistica_y_transporte.Areas.Identity.Pages.Account.ResetPasswordModel.InputModel()})
            foreach(var sample in new[]{("Abcd123!",true),("Ab1!xyz",false),("Abcdefg!",false),("abcdef1!",false),("ABCDEF1!",false),("Abcdef12",false),("Abcdef1 ",false)})
            {
                var context=new System.ComponentModel.DataAnnotations.ValidationContext(model){MemberName="Password"};
                Check(System.ComponentModel.DataAnnotations.Validator.TryValidateProperty(sample.Item1,context,new List<System.ComponentModel.DataAnnotations.ValidationResult>())==sample.Item2,"reglas de contraseña en registro y recuperación");
            }
            var policy=services.GetRequiredService<Microsoft.Extensions.Options.IOptions<IdentityOptions>>().Value.Password;
            Check(policy.RequiredLength==8&&policy.RequireDigit&&policy.RequireUppercase&&policy.RequireLowercase&&policy.RequireNonAlphanumeric,"política de Identity conserva los cinco requisitos");
            var a=Guid.NewGuid();var b=Guid.NewGuid();
            using(var scope=services.CreateScope())
            {
                var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
                Check(db.Database.ProviderName=="Microsoft.EntityFrameworkCore.Sqlite","solo proveedor de prueba");
                await db.Database.EnsureCreatedAsync();
                var users=scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
                foreach(var id in new[]{"a","b","sin-permiso","lector","admin"})
                {
                    var u=new IdentityUser{Id=id,UserName=id+"@example.test",Email=id+"@example.test",TwoFactorEnabled=true,LockoutEnabled=true};
                    Check((await users.CreateAsync(u)).Succeeded,"crear usuario ficticio");
                    var permisos=id=="sin-permiso"?Array.Empty<string>():id=="lector"?[PermisosPersonas.Leer]:PermisosPersonas.Valores.Where(p=>id=="admin"||p!=PermisosPersonas.Todos).ToArray();
                    Check((await users.AddClaimsAsync(u,permisos.Select(p=>new Claim(PermisosPersonas.Tipo,p)))).Succeeded,"claims ficticios");
                }
                db.Personas.AddRange(new Persona{personaId=a,Nombre="PrivadoA",Apellido="Ficticio",UserId="a"},
                    new Persona{personaId=b,Nombre="PrivadoB",Apellido="Ficticio",UserId="b"});await db.SaveChangesAsync();
            }
            foreach(var route in new[]{"/Personas","/Personas/Details/"+a,"/Personas/Create","/Personas/Edit/"+a,"/Personas/Delete/"+a})
            {
                var r=await anon.GetAsync(route);Check(r.StatusCode==HttpStatusCode.Redirect&&r.Headers.Location!.ToString().Contains("Login"),"anónimo bloqueado "+route);
            }
            var loginProbe=await anon.GetAsync("/Identity/Account/Login");Check(loginProbe.StatusCode==HttpStatusCode.OK,"login público disponible");
            Check((await anon.GetAsync("/Identity/Account/Register")).StatusCode==HttpStatusCode.OK,"registro público disponible sin permisos");
            using var sin=factory.Cliente();await sin.GetAsync("/__audit/signin/sin-permiso");
            foreach(var route in new[]{"/Personas","/Personas/Create","/Personas/Edit/"+a,"/Personas/Delete/"+a})Check(Denegado(await sin.GetAsync(route)),"cuenta sin permiso denegada");
            var sinAyuda=await sin.GetStringAsync("/Home/Ajustes");Check(sinAyuda.Contains("dc-experience-data")&&!sinAyuda.Contains("\"id\":\"personas\""),"ayuda sin permiso no ofrece Personas");
            using var owner=factory.Cliente();await owner.GetAsync("/__audit/signin/a");
            Check(Denegado(await sin.GetAsync("/Home/Cifras")),"cifras denegadas sin permiso");
            using(var json=System.Text.Json.JsonDocument.Parse(await owner.GetStringAsync("/Home/Cifras")))Check(json.RootElement.GetProperty("count").GetInt32()==1,"conteo respeta propietario");
            using(var cierre=factory.Cliente()){
                await cierre.GetAsync("/__audit/signin/a");var salida=await Post(cierre,"/Identity/Account/Logout","/Home/Ajustes",("returnUrl","https://example.invalid/"));Check(salida.StatusCode==HttpStatusCode.Redirect&&salida.Headers.Location!.ToString()=="/","cierre regresa al inicio de Logística e ignora retorno externo");Check((await cierre.GetAsync("/Home/Cifras")).StatusCode==HttpStatusCode.Redirect,"cierre invalida acceso a cifras");
            }
            var ayudaOwner=await owner.GetStringAsync("/Home/Ajustes");Check(ayudaOwner.Contains("\"id\":\"personas\""),"ayuda ofrece Personas con autorización real");
            if(args.Contains("--exportar-experiencia-ui")){var dir=Environment.GetEnvironmentVariable("DENIS_UI_EVIDENCE_DIR")??throw new InvalidOperationException("Evidence directory required");Directory.CreateDirectory(dir);await File.WriteAllTextAsync(Path.Combine(dir,"logistica-owner.html"),ayudaOwner);await File.WriteAllTextAsync(Path.Combine(dir,"logistica-sin.html"),sinAyuda);}
            var index=await owner.GetStringAsync("/Personas");Check(index.Contains("PrivadoA")&&!index.Contains("PrivadoB"),"listado aislado por propietario");
            Check((await owner.GetAsync("/Personas/Details/"+a)).StatusCode==HttpStatusCode.OK,"detalle propio permitido");
            foreach(var action in new[]{"Details","Edit","Delete"})Check((await owner.GetAsync("/Personas/"+action+"/"+b)).StatusCode==HttpStatusCode.NotFound,"ID ajeno oculto "+action);
            using var reader=factory.Cliente();await reader.GetAsync("/__audit/signin/lector");
            foreach(var route in new[]{"/Personas/Create","/Personas/Edit/"+a,"/Personas/Delete/"+a})Check(Denegado(await reader.GetAsync(route)),"lectura no concede escritura");

            async Task<HttpResponseMessage> Post(HttpClient client,string route,string form,params(string,string)[] values)
            {
                var html=await client.GetStringAsync(form);
                var token=WebUtility.HtmlDecode(Regex.Match(html,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
                Check(!string.IsNullOrEmpty(token),"antiforgery real del formulario");
                return await client.PostAsync(route,new FormUrlEncodedContent(values.Append(("__RequestVerificationToken",token)).Select(v=>new KeyValuePair<string,string>(v.Item1,v.Item2))));
            }
            // A real Identity temporary account must pass the server gate before protected work.
            using(var scope=services.CreateScope()){
                var users=scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();var u=new IdentityUser{Id="temporal-111",UserName="temporal@example.test",Email="temporal@example.test",EmailConfirmed=true,TwoFactorEnabled=true};
                Check((await users.CreateAsync(u,"Temporal111!Aa")).Succeeded,"crear cuenta temporal ficticia");
                Check((await users.AddClaimsAsync(u,new[]{new Claim(AccesosIniciales.Temporal,"1"),new Claim(PermisosPersonas.Tipo,PermisosPersonas.Leer)})).Succeeded,"marcar temporal sin ampliar permisos");
            }
            using(var temporary=factory.Cliente()){
                await temporary.GetAsync("/__audit/signin/temporal-111");var denied=await temporary.GetAsync("/Personas");Check(denied.StatusCode==HttpStatusCode.Redirect&&denied.Headers.Location!.ToString().EndsWith("/Identity/Account/CambiarTemporal"),"contraseña temporal bloquea el módulo desde servidor");
                var weak=await Post(temporary,"/Identity/Account/CambiarTemporal","/Identity/Account/CambiarTemporal",("Input.Actual","Temporal111!Aa"),("Input.Nueva","weak"),("Input.Confirmacion","weak"));Check(weak.StatusCode==HttpStatusCode.OK,"contraseña débil no completa cambio");
                var changed=await Post(temporary,"/Identity/Account/CambiarTemporal","/Identity/Account/CambiarTemporal",("Input.Actual","Temporal111!Aa"),("Input.Nueva","Definitiva222!Bb"),("Input.Confirmacion","Definitiva222!Bb"));Check(changed.StatusCode==HttpStatusCode.Redirect&&changed.Headers.Location!.ToString().EndsWith("/Identity/Account/Login"),"cambio válido exige sesión nueva");
                using var scope=services.CreateScope();var users=scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();var u=(await users.FindByIdAsync("temporal-111"))!;Check(await users.CheckPasswordAsync(u,"Definitiva222!Bb")&&!await users.CheckPasswordAsync(u,"Temporal111!Aa"),"hash Identity reemplazado y clave temporal inválida");Check(!(await users.GetClaimsAsync(u)).Any(c=>c.Type==AccesosIniciales.Temporal),"marca temporal retirada solo después del cambio válido");
                Check((await temporary.GetAsync("/Home/Ajustes")).StatusCode==HttpStatusCode.Redirect,"sesión antigua cerrada después del cambio");
            }
            var edit=await Post(owner,"/Personas/Edit/"+a,"/Personas/Edit/"+a,("PersonaId",a.ToString()),("Nombre","EditadoA"),("Apellido","Ficticio"),("UserId","b"),("Inactivo","true"));
            Check(edit.StatusCode==HttpStatusCode.Redirect,"edición propia válida");
            var foreignEdit=await Post(owner,"/Personas/Edit/"+b,"/Personas/Edit/"+a,("PersonaId",b.ToString()),("Nombre","Intruso"),("Apellido","Ficticio"));
            Check(foreignEdit.StatusCode==HttpStatusCode.NotFound,"POST edición ajena bloqueado");
            Check((await Post(owner,"/Personas/Delete/"+b,"/Personas/Edit/"+a)).StatusCode==HttpStatusCode.NotFound,"POST borrado ajeno bloqueado");
            Check((await Post(owner,"/Personas/Create","/Personas/Create",("Nombre","NuevoA"),("Apellido","Ficticio"),("UserId","b"),("Inactivo","true"))).StatusCode==HttpStatusCode.Redirect,"alta propia");
            Check((await owner.PostAsync("/Personas/Delete/"+a,new FormUrlEncodedContent([]))).StatusCode==HttpStatusCode.BadRequest,"sin antiforgery no escribe");
            using(var scope=services.CreateScope())
            {
                var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var own=await db.Personas.FindAsync(a);var other=await db.Personas.FindAsync(b);
                Check(own!.UserId=="a"&&!own.Inactivo&&own.Nombre=="EditadoA","overposting no cambia propietario/estado");
                Check(other!.Nombre=="PrivadoB"&&other.UserId=="b","datos ajenos intactos");
                var nueva=await db.Personas.SingleAsync(p=>p.Nombre=="NuevoA");Check(nueva.UserId=="a"&&!nueva.Inactivo,"alta toma propietario de sesión");
            }
            using var admin=factory.Cliente();await admin.GetAsync("/__audit/signin/admin");
            var all=await admin.GetStringAsync("/Personas");Check(all.Contains("EditadoA")&&all.Contains("PrivadoB"),"alcance global solo con permiso explícito");
            Check((await Post(admin,"/Personas/Edit/"+b,"/Personas/Edit/"+b,("PersonaId",b.ToString()),("Nombre","EditadoB"),("Apellido","Ficticio"),("UserId","admin"))).StatusCode==HttpStatusCode.Redirect,"administrador edita dentro de alcance global");
            Check((await Post(owner,"/Personas/Delete/"+a,"/Personas/Delete/"+a)).StatusCode==HttpStatusCode.Redirect,"borrado propio autorizado");
            using(var scope=services.CreateScope())
            {
                var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();Check(await db.Personas.FindAsync(a) is null,"borrado propio persistido");
                Check((await db.Personas.FindAsync(b))!.UserId=="b","ni administrador reasigna propietario por formulario");
                var writer=new StringWriter();
                Check(await PermisosPersonas.Administrar(scope.ServiceProvider,["--usuario-id","sin-permiso","--confirmar-usuario","otro","--accion","conceder"],writer)==2,"comando exige confirmación exacta");
                Check(await PermisosPersonas.Administrar(scope.ServiceProvider,["--usuario-id","sin-permiso","--confirmar-usuario","sin-permiso","--accion","conceder"],writer)==0,"concesión administrativa explícita");
                Check(await PermisosPersonas.Administrar(scope.ServiceProvider,["--usuario-id","sin-permiso","--confirmar-usuario","sin-permiso","--accion","revocar"],writer)==0,"revocación administrativa explícita");
            }
            using(var scope=services.CreateScope())
            {
                var users=scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
                var u=new IdentityUser{Id="abuso",UserName="abuso@example.test",Email="abuso@example.test",EmailConfirmed=true,LockoutEnabled=true};
                Check((await users.CreateAsync(u,"TestOnly!234")).Succeeded,"cuenta ficticia con contraseña");
                var signin=scope.ServiceProvider.GetRequiredService<SignInManager<IdentityUser>>();
                for(var i=0;i<5;i++)await signin.CheckPasswordSignInAsync(u,"Wrong!234",true);
                Check(await users.IsLockedOutAsync(u),"cinco fallos bloquean la cuenta");
                Check((await signin.CheckPasswordSignInAsync(u,"TestOnly!234",true)).IsLockedOut,"contraseña correcta no elude bloqueo");
                Check(await PermisosPersonas.Administrar(scope.ServiceProvider,["--usuario-id","abuso","--confirmar-usuario","abuso","--accion","conceder"],new StringWriter())==2,"sin 2FA no concede privilegios");
                var ownerUser=(await users.FindByIdAsync("b"))!;
                await users.SetTwoFactorEnabledAsync(ownerUser,false);
            }
            using(var noMfa=factory.Cliente())
            {
                await noMfa.GetAsync("/__audit/signin/b");
                Check(Denegado(await noMfa.GetAsync("/Personas")),"desactivar 2FA revoca acceso a Personas aunque conserve claims");
            }
            string mfaCode;
            using(var scope=services.CreateScope())
            {
                var users=scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
                var user=new IdentityUser{Id="mfa",UserName="mfa@example.test",Email="mfa@example.test",EmailConfirmed=true,LockoutEnabled=true};
                Check((await users.CreateAsync(user,"FixtureOnly!234")).Succeeded,"alta ficticia para TOTP");
                await users.ResetAuthenticatorKeyAsync(user);var key=(await users.GetAuthenticatorKeyAsync(user))!;
                var alphabet="ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";var bits=0;var buffer=0;var bytes=new List<byte>();
                foreach(var ch in key.ToUpperInvariant()){buffer=(buffer<<5)|alphabet.IndexOf(ch);bits+=5;if(bits>=8){bits-=8;bytes.Add((byte)(buffer>>bits));}}
                var counter=BitConverter.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeSeconds()/30);if(BitConverter.IsLittleEndian)Array.Reverse(counter);
                var hash=HMACSHA1.HashData(bytes.ToArray(),counter);var offset=hash[^1]&15;
                mfaCode=((((hash[offset]&127)<<24)|(hash[offset+1]<<16)|(hash[offset+2]<<8)|hash[offset+3])%1000000).ToString("D6");
                Check(await users.VerifyTwoFactorTokenAsync(user,users.Options.Tokens.AuthenticatorTokenProvider,mfaCode),"TOTP real aceptado");
                Check(!await users.VerifyTwoFactorTokenAsync(user,users.Options.Tokens.AuthenticatorTokenProvider,"invalid"),"TOTP inválido rechazado");
                await users.SetTwoFactorEnabledAsync(user,true);
                var codes=(await users.GenerateNewTwoFactorRecoveryCodesAsync(user,3))!.ToArray();
                Check((await users.RedeemTwoFactorRecoveryCodeAsync(user,codes[0])).Succeeded,"recuperación real por código");
                Check(!(await users.RedeemTwoFactorRecoveryCodeAsync(user,codes[0])).Succeeded,"código de recuperación de un solo uso");
                Check(await PermisosPersonas.Administrar(scope.ServiceProvider,["--usuario-id","mfa","--confirmar-usuario","mfa","--accion","conceder"],new StringWriter())==0,"privilegio tras enrolamiento MFA");
            }
            using(var mfa=factory.Cliente())
            {
                var challenge=await Post(mfa,"/Identity/Account/Login","/Identity/Account/Login",("Input.Email","mfa@example.test"),("Input.Password","FixtureOnly!234"));
                Check(challenge.StatusCode==HttpStatusCode.Redirect&&challenge.Headers.Location!.ToString().Contains("LoginWith2fa"),"contraseña no basta para iniciar sesión MFA");
                Check((await mfa.GetAsync("/Personas")).StatusCode==HttpStatusCode.Redirect,"antes del TOTP no hay sesión de aplicación");
                var done=await Post(mfa,"/Identity/Account/LoginWith2fa","/Identity/Account/LoginWith2fa",("Input.TwoFactorCode",mfaCode));
                Check(done.StatusCode==HttpStatusCode.Redirect,"TOTP completa login HTTP");
                Check((await mfa.GetAsync("/Personas")).StatusCode==HttpStatusCode.OK,"sesión MFA accede con permiso explícito");
            }
            using(var limited=factory.Cliente())
            {
                var rejected=false;
                for(var i=0;i<25;i++){limited.DefaultRequestHeaders.Remove("X-Forwarded-For");limited.DefaultRequestHeaders.Add("X-Forwarded-For","203.0.113."+(i+1));var response=await limited.GetAsync("/Identity/Account/Login");if((int)response.StatusCode==429){rejected=true;Check(response.Headers.RetryAfter is not null,"límite informa espera");break;}}
                Check(rejected,"cabecera falsa no elude límite del cliente");
                Check((await limited.GetAsync("/Home/Privacy")).StatusCode!=HttpStatusCode.TooManyRequests,"límite no agota navegación normal");
            }
            foreach(var prefix in new[]{"/deniscorp/logistica-transporte","/otra/logistica"})
            {
                await using var prefixed=new HostAuditoria(prefix);using var client=prefixed.Cliente();
                using(var scope=prefixed.Services.CreateScope())await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
                var response=await client.GetAsync(prefix+"/Identity/Account/Login");
                Check(response.StatusCode==HttpStatusCode.OK,"Identity bajo prefijo alternativo");
                var html=await response.Content.ReadAsStringAsync();
                Check(html.Contains(prefix+"/lib/")&&html.Contains(prefix+"/Identity/Account/"),"recursos y navegación bajo prefijo");
                Check(response.Headers.GetValues("Set-Cookie").All(c=>c.Contains("path="+prefix,StringComparison.OrdinalIgnoreCase)&&!c.Contains("domain=",StringComparison.OrdinalIgnoreCase)),"cookie host-only con prefijo");
                Check((await client.GetAsync("/Identity/Account/Login")).StatusCode==HttpStatusCode.NotFound,"raíz ajena al prefijo no existe");
                var challenge=await client.GetAsync(prefix+"/Personas");Check(challenge.Headers.Location!.AbsolutePath==prefix+"/Identity/Account/Login","challenge Identity conserva prefijo");
            }
            foreach(var prefix in new[]{"", "/deniscorp/logistica-transporte", "/otra/logistica"})
            {
                await using var localized=new HostAuditoria(prefix);using var client=localized.Cliente();
                foreach(var language in new[]{"es","en"})
                {
                    var r=await client.GetAsync(prefix+"/Home/Blog?culture="+(language=="en"?"en-US":"es-GT")+"&ui-culture="+language);
                    var html=WebUtility.HtmlDecode(await r.Content.ReadAsStringAsync());
                    Check(r.StatusCode==HttpStatusCode.OK,"blog público sin consultar datos");
                    Check(html.Contains("lang=\""+language+"\""),"idioma de documento");
                    Check(html.Contains(language=="en"?"How to use Logistics":"Cómo usar Logística"),"blog bilingüe");
                    Check(html.Contains(prefix+"/Home/Idioma")&&html.Contains("__RequestVerificationToken"),"selector CSRF con prefijo");
                    Check(html.Contains(prefix+"/Personas")&&html.Contains(prefix+"/js/preferences.js"),"enlaces y recursos locales");
                    Check(!html.Contains("SLA: 99.9%")&&!html.Contains("cdn.jsdelivr"),"sin indicadores ficticios ni CDN");
                    Check((await client.PostAsync(prefix+"/Home/Idioma",new FormUrlEncodedContent(new Dictionary<string,string>{{"idioma","en"},{"volver","https://example.test"}}))).StatusCode==HttpStatusCode.BadRequest,"cambio idioma requiere CSRF");
                }
                var choiceHtml=await (await client.GetAsync(prefix+"/Home/Blog")).Content.ReadAsStringAsync();
                var choiceToken=WebUtility.HtmlDecode(Regex.Match(choiceHtml,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
                var choice=await client.PostAsync(prefix+"/Home/Idioma",new FormUrlEncodedContent(new Dictionary<string,string>{{"__RequestVerificationToken",choiceToken},{"idioma","en"},{"volver",prefix+"/Home/Blog"}}));
                Check(choice.StatusCode==HttpStatusCode.Redirect&&choice.Headers.Location!.ToString()==prefix+"/Home/Blog","preferencia de idioma conserva retorno local");
                Check(choice.Headers.GetValues("Set-Cookie").Any(x=>x.Contains(".AspNetCore.Culture=")&&x.Contains("path="+(prefix.Length>0?prefix:"/"),StringComparison.OrdinalIgnoreCase)&&x.Contains("httponly",StringComparison.OrdinalIgnoreCase)),"cookie de idioma privada y limitada al prefijo");
                var remembered=await (await client.GetAsync(prefix+"/Home/Blog")).Content.ReadAsStringAsync();
                Check(remembered.Contains("lang=\"en\"")&&remembered.Contains("How to use Logistics"),"idioma persiste al navegar");
                var loginHtml=await (await client.GetAsync(prefix+"/Identity/Account/Login")).Content.ReadAsStringAsync();
                Check(loginHtml.Contains("lang=\"en\"")&&loginHtml.Contains("action=\""+prefix+"/Home/Idioma\""),"selector correcto dentro del área Identity");
                var unsafeReturn=await client.PostAsync(prefix+"/Home/Idioma",new FormUrlEncodedContent(new Dictionary<string,string>{{"__RequestVerificationToken",choiceToken},{"idioma","es"},{"volver","https://example.test"}}));
                Check(unsafeReturn.StatusCode==HttpStatusCode.Redirect&&!unsafeReturn.Headers.Location!.IsAbsoluteUri,"rechaza retorno a sitio externo");
            }
            Console.WriteLine($"OK: {checks} comprobaciones HTTP de autorización, alcance, sobreescritura, CSRF y permisos. SQL Server no fue abierto.");
        }
        finally { Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection",anterior); }
    }
}

internal sealed class HostAuditoria : WebApplicationFactory<Program>
{
    private readonly SqliteConnection connection=new("Data Source=:memory:");
    private readonly string prefix;
    public HostAuditoria(string prefix=""){this.prefix=prefix;connection.Open();}
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseContentRoot(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../")));
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_,c)=>c.AddInMemoryCollection(new Dictionary<string,string?>{["Hosting:PathBase"]=prefix}));
        builder.ConfigureLogging(logging=>logging.ClearProviders());
        builder.ConfigureServices(services=>
        {
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.RemoveAll<DbContextOptions<AppDbContext>>();services.RemoveAll<AppDbContext>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(o=>o.UseSqlite(connection));
            services.AddTransient<IStartupFilter,LoginFicticio>();
        });
    }
    public HttpClient Cliente()=>CreateClient(new(){BaseAddress=new Uri("https://localhost"),AllowAutoRedirect=false});
    protected override void Dispose(bool disposing){base.Dispose(disposing);if(disposing)connection.Dispose();}
}
internal sealed class LoginFicticio : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)=>app=>
    {
        app.Use(async(context,siguiente)=>
        {
            context.Connection.RemoteIpAddress=IPAddress.Parse("198.51.100.10");
            if(context.Request.Path.StartsWithSegments("/__audit/signin",out var rest))
            {
                var users=context.RequestServices.GetRequiredService<UserManager<IdentityUser>>();
                var u=await users.FindByIdAsync(rest.Value!.Trim('/'))??throw new Exception("Usuario de prueba inexistente");
                await context.RequestServices.GetRequiredService<SignInManager<IdentityUser>>().SignInAsync(u,false);
                context.Response.StatusCode=204;return;
            }
            await siguiente();
        });next(app);
    };
}
