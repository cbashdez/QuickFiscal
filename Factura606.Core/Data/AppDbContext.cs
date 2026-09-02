using Factura606.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Factura606.Core.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<FacturaExtraida> Facturas => Set<FacturaExtraida>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FacturaExtraida>(e =>
        {
            e.HasIndex(f => f.Ncf);
        });
    }

    /// <summary>
    /// Crea el archivo/esquema si no existe. EF Core's <c>EnsureCreated</c> no migra una
    /// base ya existente — si en el futuro se agrega una columna, hace falta un
    /// <c>ALTER TABLE</c> manual explícito aquí (ver sección 2.3 del documento maestro,
    /// gotcha ya vivido una vez con <c>DudasJson</c>).
    /// </summary>
    public void AsegurarBaseDeDatos()
    {
        Database.EnsureCreated();
    }
}
