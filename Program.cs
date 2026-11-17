using Logistica_y_transporte.Models;
using Logistica_y_transporte.Seguridad;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder=WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages(options=>
{
    foreach(var pagina in new[]{"Login","Register","ForgotPassword","ForgotPasswordConfirmation",
        "ResetPassword","ResetPasswordConfirmation","RegisterConfirmation","ConfirmEmail",
        "ConfirmEmailChange","ResendEmailConfirmation","ExternalLogin","LoginWith2fa","LoginWithRecoveryCode"})
        options.Conventions.AllowAnonymousToAreaPage("Identity","/Account/"+pagina);
});
var connectionString=builder.Configuration.GetConnectionString("DefaultConnection");
if(string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Falta ConnectionStrings:DefaultConnection. Configúrala en User Secrets o en el entorno privado.");
builder.Services.AddDbContext<AppDbContext>(options=>options.UseSqlServer(connectionString));
builder.Services.AddDefaultIdentity<IdentityUser>(options=>options.SignIn.RequireConfirmedAccount=false)
    .AddEntityFrameworkStores<AppDbContext>();
builder.Services.AddAuthorization(PermisosPersonas.Configurar);
builder.Services.Configure<SecurityStampValidatorOptions>(o=>o.ValidationInterval=TimeSpan.Zero);
var app=builder.Build();
if(args.Contains("--administrar-personas"))
{
    using var scope=app.Services.CreateScope();
    Environment.ExitCode=await PermisosPersonas.Administrar(scope.ServiceProvider,args,Console.Out);
    return;
}
if(!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Home/Error");app.UseHsts(); }
app.UseHttpsRedirection();app.UseStaticFiles();app.UseRouting();
app.UseAuthentication();app.UseAuthorization();
app.MapControllerRoute("areas","{area:exists}/{controller=Home}/{action=Index}/{id?}");
app.MapControllerRoute("default","{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();app.Run();

public partial class Program { }
