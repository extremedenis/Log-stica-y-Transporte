using System.ComponentModel.DataAnnotations;

namespace Logistica_y_transporte.Models;

// El propietario y los campos de persistencia no forman parte del contrato de escritura.
public sealed class PersonaFormulario
{
    public Guid PersonaId { get; set; }
    [Required, StringLength(100)] public string Nombre { get; set; } = "";
    [Required, StringLength(100)] public string Apellido { get; set; } = "";
    [StringLength(500)] public string? Direccion { get; set; }
}
