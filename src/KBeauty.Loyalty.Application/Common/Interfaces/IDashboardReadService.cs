using KBeauty.Loyalty.Application.Admin.Queries.GetAdminDashboard;
using KBeauty.Loyalty.Application.Admin.Queries.GetDashboardSummary;

namespace KBeauty.Loyalty.Application.Common.Interfaces;

/// <summary>
/// Lecturas optimizadas para el dashboard administrativo. Existe como servicio
/// separado (en vez de bloating de los repositorios) porque hace agregaciones
/// que no encajan en CRUD por entidad.
/// </summary>
/// <remarks>
/// La implementación en Infrastructure usa <c>AsNoTracking</c> + <c>GroupBy</c>
/// con proyecciones para minimizar columnas leídas.
/// </remarks>
public interface IDashboardReadService
{
    /// <summary>Construye el <see cref="DashboardDto"/> con conteos y agregaciones del mes en curso.</summary>
    Task<DashboardDto> GetDashboardAsync(CancellationToken ct = default);

    /// <summary>Construye el dashboard analitico de Fase 3.1.</summary>
    Task<DashboardSummaryDto> GetDashboardSummaryAsync(CancellationToken ct = default);
}
