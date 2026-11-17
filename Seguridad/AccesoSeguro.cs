using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;

namespace Logistica_y_transporte.Seguridad;

public sealed class SegundoFactorPrivilegiado : IAuthorizationRequirement { }
public sealed class VerificarSegundoFactor(UserManager<IdentityUser> users) : AuthorizationHandler<SegundoFactorPrivilegiado>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, SegundoFactorPrivilegiado requirement)
    {
        var id=context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if(id is not null && await users.FindByIdAsync(id) is {} user && await users.GetTwoFactorEnabledAsync(user))
            context.Succeed(requirement);
    }
}
public static class AccesoSeguro
{
    public static bool EsAcceso(PathString path) => path.StartsWithSegments("/Identity/Account") && !path.StartsWithSegments("/Identity/Account/Manage");
    public static void Configurar(IServiceCollection services,IConfiguration config)
    {
        services.AddScoped<IAuthorizationHandler,VerificarSegundoFactor>();
        services.Configure<ForwardedHeadersOptions>(o=>
        {
            o.ForwardedHeaders=ForwardedHeaders.XForwardedFor|ForwardedHeaders.XForwardedProto;
            o.ForwardLimit=1;
            // No se borran las listas seguras de ASP.NET. Cada proxy adicional debe ser una IP exacta.
            foreach(var value in config.GetSection("Hosting:KnownProxies").Get<string[]>()??[])
                o.KnownProxies.Add(IPAddress.Parse(value));
        });
        services.AddRateLimiter(o=>
        {
            o.RejectionStatusCode=429;
            o.GlobalLimiter=PartitionedRateLimiter.CreateChained(
                PartitionedRateLimiter.Create<HttpContext,string>(c=>EsAcceso(c.Request.Path)
                    ? RateLimitPartition.GetFixedWindowLimiter("access-total",_=>new(){PermitLimit=100,Window=TimeSpan.FromMinutes(1),QueueLimit=0})
                    : RateLimitPartition.GetNoLimiter("other")),
                PartitionedRateLimiter.Create<HttpContext,string>(c=>EsAcceso(c.Request.Path)
                    ? RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new(){PermitLimit=20,Window=TimeSpan.FromMinutes(1),QueueLimit=0})
                    : RateLimitPartition.GetNoLimiter("other")));
            o.OnRejected=async (context,ct)=>
            {
                context.HttpContext.Response.Headers.RetryAfter="60";
                context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Seguridad.Acceso").LogWarning(new EventId(7101,"LimiteAcceso"),"Límite de acceso alcanzado; no se registran cuenta, IP ni credenciales.");
                await context.HttpContext.Response.WriteAsync("Demasiados intentos. Espera un minuto. / Too many attempts. Wait one minute.",ct);
            };
        });
    }
}
