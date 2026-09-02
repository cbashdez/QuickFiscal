using Factura606.Core.Data;
using Factura606.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Factura606.Core.Services;

public record ResultadoAgregar(FacturaExtraida Factura, bool NcfDuplicado);

/// <summary>
/// Operaciones de alto nivel sobre las facturas cargadas — capa que consumen los
/// componentes Blazor. Envuelve el DbContext para no repetir las reglas de negocio
/// (detección de duplicados, sincronizar MontoFacturado con Total, etc.) en cada página.
/// </summary>
public class FacturaService(AppDbContext db)
{
    public Task<List<FacturaExtraida>> ListarAsync(CancellationToken ct = default) =>
        db.Facturas.OrderByDescending(f => f.FechaCarga).ToListAsync(ct);

    public Task<FacturaExtraida?> ObtenerAsync(int id, CancellationToken ct = default) =>
        db.Facturas.FirstOrDefaultAsync(f => f.Id == id, ct);

    /// <summary>
    /// Revisa si ya existe una factura con el mismo NCF antes de agregar — no bloquea,
    /// solo informa, para que la UI pueda preguntar "¿la subiste dos veces?" (sección 2.8).
    /// </summary>
    public async Task<bool> ExisteNcfAsync(string? ncf, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ncf)) return false;
        return await db.Facturas.AnyAsync(f => f.Ncf == ncf, ct);
    }

    public async Task<ResultadoAgregar> AgregarAsync(FacturaExtraida factura, CancellationToken ct = default)
    {
        var duplicado = await ExisteNcfAsync(factura.Ncf, ct);
        db.Facturas.Add(factura);
        await db.SaveChangesAsync(ct);
        return new ResultadoAgregar(factura, duplicado);
    }

    public async Task ActualizarAsync(FacturaExtraida factura, CancellationToken ct = default)
    {
        // Regla de negocio (sección 2.3): si el usuario corrige el total a mano,
        // MontoFacturado se mantiene sincronizado con él.
        factura.MontoFacturado = factura.Total;
        db.Facturas.Update(factura);
        await db.SaveChangesAsync(ct);
    }

    public async Task EliminarAsync(IEnumerable<int> ids, CancellationToken ct = default)
    {
        var aBorrar = await db.Facturas.Where(f => ids.Contains(f.Id)).ToListAsync(ct);
        db.Facturas.RemoveRange(aBorrar);
        await db.SaveChangesAsync(ct);
    }
}
