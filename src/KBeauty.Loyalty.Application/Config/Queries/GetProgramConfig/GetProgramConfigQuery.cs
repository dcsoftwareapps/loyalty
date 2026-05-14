using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Config.Queries.GetProgramConfig;

/// <summary>Todas las reglas vigentes del programa para mostrar en el panel admin.</summary>
public sealed record GetProgramConfigQuery : IRequest<Result<IReadOnlyList<ConfigDto>>>;
