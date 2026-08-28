using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Admin.Queries.AdvancedReports;

public sealed record GetTopCustomersReportQuery(DateTime StartUtc, DateTime EndUtc, TopCustomerMetric Metric, string? Level = null, int Limit = 50) : IRequest<Result<TopCustomersReportDto>>;
public sealed record GetVisitFrequencyReportQuery(DateTime StartUtc, DateTime EndUtc, int Limit = 100) : IRequest<Result<VisitFrequencyReportDto>>;
public sealed record GetReturningCustomersReportQuery(DateTime StartUtc, DateTime EndUtc) : IRequest<Result<ReturningCustomersReportDto>>;
public sealed record GetActivityTrendsReportQuery(DateTime StartUtc, DateTime EndUtc) : IRequest<Result<ActivityTrendsReportDto>>;
public sealed record GetLevelDistributionReportQuery() : IRequest<Result<LevelDistributionReportDto>>;

internal static class AdvancedReportValidation
{
    public static string? Validate(DateTime startUtc, DateTime endUtc) =>
        startUtc >= endUtc ? "La fecha de inicio debe ser anterior a la fecha fin." :
        endUtc - startUtc > TimeSpan.FromDays(1100) ? "El rango máximo es de 3 años." : null;
}

public sealed class GetTopCustomersReportHandler(IReportsReadService read) : IRequestHandler<GetTopCustomersReportQuery, Result<TopCustomersReportDto>>
{
    public async Task<Result<TopCustomersReportDto>> Handle(GetTopCustomersReportQuery query, CancellationToken ct) =>
        AdvancedReportValidation.Validate(query.StartUtc, query.EndUtc) is { } error ? Result.Fail<TopCustomersReportDto>(error) : Result.Ok(await read.GetTopCustomersAsync(query, ct));
}
public sealed class GetVisitFrequencyReportHandler(IReportsReadService read) : IRequestHandler<GetVisitFrequencyReportQuery, Result<VisitFrequencyReportDto>>
{
    public async Task<Result<VisitFrequencyReportDto>> Handle(GetVisitFrequencyReportQuery query, CancellationToken ct) =>
        AdvancedReportValidation.Validate(query.StartUtc, query.EndUtc) is { } error ? Result.Fail<VisitFrequencyReportDto>(error) : Result.Ok(await read.GetVisitFrequencyAsync(query, ct));
}
public sealed class GetReturningCustomersReportHandler(IReportsReadService read) : IRequestHandler<GetReturningCustomersReportQuery, Result<ReturningCustomersReportDto>>
{
    public async Task<Result<ReturningCustomersReportDto>> Handle(GetReturningCustomersReportQuery query, CancellationToken ct) =>
        AdvancedReportValidation.Validate(query.StartUtc, query.EndUtc) is { } error ? Result.Fail<ReturningCustomersReportDto>(error) : Result.Ok(await read.GetReturningCustomersAsync(query, ct));
}
public sealed class GetActivityTrendsReportHandler(IReportsReadService read) : IRequestHandler<GetActivityTrendsReportQuery, Result<ActivityTrendsReportDto>>
{
    public async Task<Result<ActivityTrendsReportDto>> Handle(GetActivityTrendsReportQuery query, CancellationToken ct) =>
        AdvancedReportValidation.Validate(query.StartUtc, query.EndUtc) is { } error ? Result.Fail<ActivityTrendsReportDto>(error) : Result.Ok(await read.GetActivityTrendsAsync(query, ct));
}
public sealed class GetLevelDistributionReportHandler(IReportsReadService read) : IRequestHandler<GetLevelDistributionReportQuery, Result<LevelDistributionReportDto>>
{
    public async Task<Result<LevelDistributionReportDto>> Handle(GetLevelDistributionReportQuery query, CancellationToken ct) => Result.Ok(await read.GetLevelDistributionAsync(query, ct));
}