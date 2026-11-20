using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
namespace Logistica_y_transporte.Seguridad;
public static class AccesosIniciales{
 public const string Temporal="logistica:clave-temporal";
 public static async Task<int> Preparar(IServiceProvider services,string file){
  var users=services.GetRequiredService<UserManager<IdentityUser>>();var rows=JsonSerializer.Deserialize<Entrada[]>(await File.ReadAllTextAsync(file))??[];
  if(rows.Length!=2||rows.Any(r=>r.Role is not ("SuperAdmin" or "Cliente"))||rows.Select(r=>r.Email).Distinct().Count()!=2)throw new InvalidOperationException("Formato de accesos no válido.");
  foreach(var row in rows){var found=await users.FindByEmailAsync(row.Email);if(found is not null){Console.WriteLine("Cuenta existente conservada.");continue;}
   var user=new IdentityUser{UserName=row.Email,Email=row.Email,EmailConfirmed=true};var result=await users.CreateAsync(user,row.Password);if(!result.Succeeded)throw new InvalidOperationException("No se pudo crear el acceso; revisa las reglas sin registrar la contraseña.");
   var claims=new List<Claim>{new(ClaimTypes.Role,row.Role),new(Temporal,"1")};claims.AddRange(PermisosPersonas.Valores.Where(p=>row.Role=="SuperAdmin"||p!=PermisosPersonas.Todos).Select(p=>new Claim(PermisosPersonas.Tipo,p)));if(!(await users.AddClaimsAsync(user,claims)).Succeeded)throw new InvalidOperationException("No se pudieron guardar permisos.");Console.WriteLine("Cuenta inicial creada; contraseña temporal y 2FA pendientes.");
  }return 0;
 }
 public record Entrada(string Email,string Password,string Role);
}
