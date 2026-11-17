using System.Net;
using System.Security.Claims;
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

internal sealed class AuditoriaRunner
{
    public static async Task Main()
    {
        // No usa la conexión privada ni abre SQL Server. El proveedor real se sustituye antes de crear el host.
        var anterior=Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection","Server=127.0.0.1,1;Database=AuditUnused;Integrated Security=true;Connect Timeout=1");
        try
        {
            await using var factory=new HostAuditoria();
            using var anon=factory.Cliente();var services=factory.Services;var checks=0;
            void Check(bool ok,string message){if(!ok)throw new Exception("FALLA: "+message);checks++;}
            bool Denegado(HttpResponseMessage r)=>r.StatusCode==HttpStatusCode.Forbidden ||
                (r.StatusCode==HttpStatusCode.Redirect&&r.Headers.Location?.ToString().Contains("AccessDenied")==true);
            var a=Guid.NewGuid();var b=Guid.NewGuid();
            using(var scope=services.CreateScope())
            {
                var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
                Check(db.Database.ProviderName=="Microsoft.EntityFrameworkCore.Sqlite","solo proveedor de prueba");
                await db.Database.EnsureCreatedAsync();
                var users=scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
                foreach(var id in new[]{"a","b","sin-permiso","lector","admin"})
                {
                    var u=new IdentityUser{Id=id,UserName=id+"@example.test",Email=id+"@example.test"};
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
            Check((await anon.GetAsync("/Identity/Account/Login")).StatusCode==HttpStatusCode.OK,"login público disponible");
            Check((await anon.GetAsync("/Identity/Account/Register")).StatusCode==HttpStatusCode.OK,"registro público disponible sin permisos");
            using var sin=factory.Cliente();await sin.GetAsync("/__audit/signin/sin-permiso");
            foreach(var route in new[]{"/Personas","/Personas/Create","/Personas/Edit/"+a,"/Personas/Delete/"+a})Check(Denegado(await sin.GetAsync(route)),"cuenta sin permiso denegada");
            using var owner=factory.Cliente();await owner.GetAsync("/__audit/signin/a");
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
            Console.WriteLine($"OK: {checks} comprobaciones HTTP de autorización, alcance, sobreescritura, CSRF y permisos. SQL Server no fue abierto.");
        }
        finally { Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection",anterior); }
    }
}

internal sealed class HostAuditoria : WebApplicationFactory<Program>
{
    private readonly SqliteConnection connection=new("Data Source=:memory:");
    public HostAuditoria()=>connection.Open();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseContentRoot(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../")));
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging=>logging.ClearProviders());
        builder.ConfigureServices(services=>
        {
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
