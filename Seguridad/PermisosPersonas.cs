using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace Logistica_y_transporte.Seguridad;

public static class PermisosPersonas
{
    public const string Tipo = "logistica:permiso";
    public const string Leer = "Personas.Leer";
    public const string Crear = "Personas.Crear";
    public const string Editar = "Personas.Editar";
    public const string Eliminar = "Personas.Eliminar";
    public const string Todos = "Personas.Todos";
    public static readonly string[] Valores = [Leer, Crear, Editar, Eliminar, Todos];

    public static void Configurar(AuthorizationOptions options)
    {
        options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        foreach (var permiso in Valores)
            options.AddPolicy(permiso, p => p.RequireAuthenticatedUser()
                .RequireClaim(ClaimTypes.NameIdentifier).RequireClaim(Tipo, permiso).AddRequirements(new SegundoFactorPrivilegiado()));
    }

    // Herramienta de administración local. El registro público nunca concede permisos.
    public static async Task<int> Administrar(IServiceProvider services, string[] args, TextWriter output)
    {
        string? Valor(string nombre) { var i=Array.IndexOf(args,nombre); return i>=0&&i+1<args.Length?args[i+1]:null; }
        var id=Valor("--usuario-id");
        var accion=Valor("--accion");
        if (string.IsNullOrWhiteSpace(id) || id!=Valor("--confirmar-usuario") || accion is not ("conceder" or "revocar"))
        { await output.WriteLineAsync("Indica --usuario-id, --confirmar-usuario con el mismo ID y --accion conceder|revocar."); return 2; }
        var manager=services.GetRequiredService<UserManager<IdentityUser>>();
        var usuario=await manager.FindByIdAsync(id);
        if(usuario is null) { await output.WriteLineAsync("La cuenta indicada no existe. No se hicieron cambios."); return 2; }
        if(accion=="conceder" && !await manager.GetTwoFactorEnabledAsync(usuario))
        {await output.WriteLineAsync("La cuenta debe configurar 2FA y guardar sus códigos de recuperación antes de recibir permisos.");return 2;}
        var actuales=await manager.GetClaimsAsync(usuario);
        var claims=accion=="conceder"
            ? Valores.Where(v=>!actuales.Any(c=>c.Type==Tipo&&c.Value==v)).Select(v=>new Claim(Tipo,v)).ToArray()
            : actuales.Where(c=>c.Type==Tipo).ToArray();
        var result=accion=="conceder"?await manager.AddClaimsAsync(usuario,claims):await manager.RemoveClaimsAsync(usuario,claims);
        if(!result.Succeeded) { await output.WriteLineAsync("No se pudieron guardar los permisos."); return 1; }
        var stamp=await manager.UpdateSecurityStampAsync(usuario);
        if(!stamp.Succeeded) { await output.WriteLineAsync("Revisa los permisos y revoca las sesiones de la cuenta antes de continuar."); return 1; }
        await output.WriteLineAsync("Permisos actualizados. La cuenta debe iniciar una sesión nueva.");
        return 0;
    }
}
