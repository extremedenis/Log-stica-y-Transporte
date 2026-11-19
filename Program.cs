using Logistica_y_transporte.Models;
using Logistica_y_transporte.Seguridad;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

var builder=WebApplication.CreateBuilder(args);
if(builder.Environment.IsDevelopment())builder.Configuration.AddJsonFile("appsettings.Postgres.local.json",optional:true,reloadOnChange:false);
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages(options=>
{
    foreach(var pagina in new[]{"Login","Register","ForgotPassword","ForgotPasswordConfirmation",
        "ResetPassword","ResetPasswordConfirmation","RegisterConfirmation","ConfirmEmail",
        "ConfirmEmailChange","ResendEmailConfirmation","ExternalLogin","LoginWith2fa","LoginWithRecoveryCode"})
        options.Conventions.AllowAnonymousToAreaPage("Identity","/Account/"+pagina);
});
var migrateDatabase=args.Contains("--migrar-db");
var connectionString=builder.Configuration.GetConnectionString(migrateDatabase?"MigrationConnection":"DefaultConnection");
if(string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Falta ConnectionStrings:DefaultConnection. Configúrala en User Secrets o en el entorno privado.");
if(appEnvironmentProduction(builder.Environment))
{
    var sql=new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
    if(sql.Host is not ("localhost" or "127.0.0.1" or "::1") && !(sql.Host?.StartsWith('/')??false) && sql.SslMode is not Npgsql.SslMode.VerifyFull)
        throw new InvalidOperationException("PostgreSQL productivo exige SSL Mode=VerifyFull.");
    var keys=builder.Configuration["DataProtection:KeysPath"];
    if(string.IsNullOrWhiteSpace(keys))throw new InvalidOperationException("Falta el volumen persistente DataProtection:KeysPath.");
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keys)).SetApplicationName("Denis.Logistica");
}
builder.Services.AddDbContext<AppDbContext>(options=>options.UseNpgsql(connectionString,sql=>sql.EnableRetryOnFailure(3)));
static bool appEnvironmentProduction(IHostEnvironment env)=>!env.IsDevelopment()&&!env.IsEnvironment("Testing");
builder.Services.AddDefaultIdentity<IdentityUser>(options=>
{
 options.Password.RequiredLength=8;
 options.Password.RequireDigit=true;options.Password.RequireUppercase=true;options.Password.RequireLowercase=true;options.Password.RequireNonAlphanumeric=true;
    options.Stores.MaxLengthForKeys=128;
    options.SignIn.RequireConfirmedAccount=!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing");
    options.Lockout.AllowedForNewUsers=true;
    options.Lockout.MaxFailedAccessAttempts=5;
    options.Lockout.DefaultLockoutTimeSpan=TimeSpan.FromMinutes(15);
})
    .AddEntityFrameworkStores<AppDbContext>();
builder.Services.AddAuthorization(PermisosPersonas.Configurar);
AccesoSeguro.Configurar(builder.Services,builder.Configuration);
builder.Services.AddAntiforgery(o=>{o.Cookie.Path=builder.Configuration["Hosting:PathBase"] is {Length:>0} prefix?prefix:"/";o.Cookie.Domain=null;});
builder.Services.ConfigureApplicationCookie(o=>{o.Cookie.Name=".Denis.Logistica.Sesion";o.Cookie.Path=builder.Configuration["Hosting:PathBase"] is {Length:>0} prefix?prefix:"/";o.Cookie.Domain=null;o.Cookie.HttpOnly=true;o.Cookie.SameSite=SameSiteMode.Lax;o.Cookie.SecurePolicy=builder.Environment.IsDevelopment()||builder.Environment.IsEnvironment("Testing")?CookieSecurePolicy.SameAsRequest:CookieSecurePolicy.Always;});
builder.Services.Configure<SecurityStampValidatorOptions>(o=>o.ValidationInterval=TimeSpan.Zero);
var app=builder.Build();
if(migrateDatabase)
{
    using var scope=app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    Console.WriteLine("PostgreSQL migrations applied. / Migraciones PostgreSQL aplicadas.");return;
}
if(args.Contains("--comprobar-db"))
{
    using var scope=app.Services.CreateScope();
    try {var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();Environment.ExitCode=await db.Database.CanConnectAsync() && !(await db.Database.GetPendingMigrationsAsync()).Any()?0:1;}
    catch {Environment.ExitCode=1;}
    Console.WriteLine(Environment.ExitCode==0?"Database reachable":"Database unavailable");return;
}
if(args.Contains("--administrar-personas"))
{
    using var scope=app.Services.CreateScope();
    Environment.ExitCode=await PermisosPersonas.Administrar(scope.ServiceProvider,args,Console.Out);
    return;
}
if(!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Home/Error");app.UseHsts(); }
app.UseForwardedHeaders();
var pathBase=builder.Configuration["Hosting:PathBase"]??"";
if(pathBase.Length>0)
{
    if(!System.Text.RegularExpressions.Regex.IsMatch(pathBase,"^(/[a-z0-9-]+)+$"))throw new InvalidOperationException("Hosting:PathBase inválido.");
    app.UsePathBase(pathBase);
    app.Use(async(c,next)=>{if(c.Request.PathBase!=pathBase){c.Response.StatusCode=404;return;}await next();});
}
app.Use(async(c,next)=>
{
    c.Response.Headers["X-Content-Type-Options"]="nosniff";
    c.Response.Headers["X-Frame-Options"]="DENY";
    c.Response.Headers["Referrer-Policy"]="no-referrer";
    // Sin remitente real no se publica el registro/recuperación de demostración de Identity.
    if(!app.Environment.IsDevelopment()&&!app.Environment.IsEnvironment("Testing") &&
        new[]{"/Identity/Account/Register","/Identity/Account/RegisterConfirmation","/Identity/Account/ForgotPassword","/Identity/Account/ResendEmailConfirmation"}.Contains(c.Request.Path.Value,StringComparer.OrdinalIgnoreCase))
    {c.Response.StatusCode=503;await c.Response.WriteAsync("Alta y recuperación por correo pendientes de configuración. / Email enrollment and recovery are not configured.");return;}
    await next();
});
app.UseRequestLocalization(new Microsoft.AspNetCore.Builder.RequestLocalizationOptions()
 .SetDefaultCulture("es-GT").AddSupportedCultures("es-GT","en-US").AddSupportedUICultures("es","en"));
app.UseHttpsRedirection();app.UseStaticFiles();app.UseRouting();app.UseRateLimiter();
app.UseAuthentication();app.UseAuthorization();
app.MapControllerRoute("areas","{area:exists}/{controller=Home}/{action=Index}/{id?}");
app.MapControllerRoute("default","{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();app.Run();

public partial class Program { }
