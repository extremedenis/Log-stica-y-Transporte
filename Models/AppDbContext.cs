using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Logistica_y_transporte.Models
{
    //Base de datos principal
    public class AppDbContext : IdentityDbContext<IdentityUser>
    {
        //Constructor
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        //Tabla de persona de la base de datos
        public DbSet<Persona> Personas { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            // Same shape in the design factory, Identity UI, maintenance CLI and tests.
            builder.Entity<IdentityUserLogin<string>>(entity =>
            {
                entity.Property(value => value.LoginProvider).HasMaxLength(128);
                entity.Property(value => value.ProviderKey).HasMaxLength(128);
            });
            builder.Entity<IdentityUserToken<string>>(entity =>
            {
                entity.Property(value => value.LoginProvider).HasMaxLength(128);
                entity.Property(value => value.Name).HasMaxLength(128);
            });
        }
    }
}
