using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Logistica_y_transporte.Models;
using Logistica_y_transporte.Seguridad;

namespace Logistica_y_transporte.Controllers;

[Authorize]
public class PersonasController(AppDbContext context) : Controller
{
    private string UsuarioId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("La sesión no contiene identificador de cuenta.");
    private IQueryable<Persona> Alcance() => User.HasClaim(PermisosPersonas.Tipo,PermisosPersonas.Todos)
        ? context.Personas : context.Personas.Where(p=>p.UserId==UsuarioId);

    [Authorize(Policy=PermisosPersonas.Leer)]
    public async Task<IActionResult> Index() => View(await Alcance().AsNoTracking().Include(p=>p.User).ToListAsync());

    [Authorize(Policy=PermisosPersonas.Leer)]
    public async Task<IActionResult> Details(Guid? id)
    {
        if(id is null)return NotFound();
        var persona=await Alcance().AsNoTracking().Include(p=>p.User).SingleOrDefaultAsync(p=>p.personaId==id);
        return persona is null ? NotFound() : View(persona);
    }

    [Authorize(Policy=PermisosPersonas.Crear)]
    public IActionResult Create() => View(new PersonaFormulario());

    [HttpPost,ValidateAntiForgeryToken,Authorize(Policy=PermisosPersonas.Crear)]
    public async Task<IActionResult> Create(PersonaFormulario formulario)
    {
        if(!ModelState.IsValid)return View(formulario);
        var persona=new Persona{personaId=Guid.NewGuid(),Nombre=formulario.Nombre,Apellido=formulario.Apellido,
            Direccion=formulario.Direccion,UserId=UsuarioId};
        context.Personas.Add(persona);
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy=PermisosPersonas.Editar)]
    public async Task<IActionResult> Edit(Guid? id)
    {
        if(id is null)return NotFound();
        var persona=await Alcance().AsNoTracking().SingleOrDefaultAsync(p=>p.personaId==id);
        return persona is null ? NotFound() : View(new PersonaFormulario{PersonaId=persona.personaId,
            Nombre=persona.Nombre??"",Apellido=persona.Apellido??"",Direccion=persona.Direccion});
    }

    [HttpPost,ValidateAntiForgeryToken,Authorize(Policy=PermisosPersonas.Editar)]
    public async Task<IActionResult> Edit(Guid id,PersonaFormulario formulario)
    {
        if(id!=formulario.PersonaId)return NotFound();
        var persona=await Alcance().SingleOrDefaultAsync(p=>p.personaId==id);
        if(persona is null)return NotFound();
        if(!ModelState.IsValid)return View(formulario);
        // Se carga el registro autorizado; nunca se adjunta una entidad suministrada por el navegador.
        persona.Nombre=formulario.Nombre;persona.Apellido=formulario.Apellido;persona.Direccion=formulario.Direccion;
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy=PermisosPersonas.Eliminar)]
    public async Task<IActionResult> Delete(Guid? id)
    {
        if(id is null)return NotFound();
        var persona=await Alcance().AsNoTracking().Include(p=>p.User).SingleOrDefaultAsync(p=>p.personaId==id);
        return persona is null ? NotFound() : View(persona);
    }

    [HttpPost,ActionName("Delete"),ValidateAntiForgeryToken,Authorize(Policy=PermisosPersonas.Eliminar)]
    public async Task<IActionResult> DeleteConfirmed(Guid id)
    {
        var persona=await Alcance().SingleOrDefaultAsync(p=>p.personaId==id);
        if(persona is null)return NotFound();
        context.Personas.Remove(persona);await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
