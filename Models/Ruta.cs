using Logistica_y_transporte.Localizacion;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Logistica_y_transporte.Models
{
    public class Ruta
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [DisplayName("ID Ruta")]
        public int id_ruta { get; set; }

        [Required(ErrorMessageResourceType=typeof(Interfaz),ErrorMessageResourceName=nameof(Interfaz.Required))]
        [Column(TypeName = "varchar(100)")]
        [DisplayName("Zona")]
        public string zona { get; set; } = string.Empty;

        [Required(ErrorMessageResourceType=typeof(Interfaz),ErrorMessageResourceName=nameof(Interfaz.Required))]
        [Column(TypeName = "varchar(100)")]
        [DisplayName("Piloto")]
        public string piloto { get; set; } = string.Empty;
    }
}