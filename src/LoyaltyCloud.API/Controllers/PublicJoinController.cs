using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Customers.Commands.JoinCustomer;
using LoyaltyCloud.Application.Customers.Commands.UpdateCustomerBirthday;
using LoyaltyCloud.Infrastructure.Configuration;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LoyaltyCloud.API.Controllers;

[ApiController]
[Route("api/public")]
[Produces("application/json")]
public sealed class PublicJoinController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IPublicTenantResolver _tenantResolver;
    private readonly IMutableTenantContext _tenantContext;
    private readonly IWebHostEnvironment _environment;
    private readonly ApplePassOptions _options;

    public PublicJoinController(
        ISender sender,
        IPublicTenantResolver tenantResolver,
        IMutableTenantContext tenantContext,
        IWebHostEnvironment environment,
        IOptions<ApplePassOptions> options)
    {
        _sender = sender;
        _tenantResolver = tenantResolver;
        _tenantContext = tenantContext;
        _environment = environment;
        _options = options.Value;
    }

    [HttpPost("{tenantSlug}/join")]
    [ProducesResponseType(typeof(PublicJoinResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Join(
        string tenantSlug,
        [FromBody] PublicJoinRequest body,
        CancellationToken ct)
    {
        var tenant = await ResolveOperationalTenantAsync(tenantSlug, ct);
        if (tenant.Result is not null)
            return tenant.Result;

        var result = await _sender.Send(new JoinCustomerCommand(
            body.FirstName,
            body.LastName,
            body.Phone), ct);

        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = $"Alta {tenant.Info!.DisplayName}", Detail = result.Error });

        return Ok(ToResponse(result.Value, tenant.Info!));
    }

    [HttpPut("{tenantSlug}/join/{serialNumber}/birthday")]
    [ProducesResponseType(typeof(PublicBirthdayResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateBirthday(
        string tenantSlug,
        string serialNumber,
        [FromBody] PublicBirthdayRequest body,
        CancellationToken ct)
    {
        var tenant = await ResolveOperationalTenantAsync(tenantSlug, ct);
        if (tenant.Result is not null)
            return tenant.Result;

        var result = await _sender.Send(new UpdateCustomerBirthdayCommand(
            CustomerId: null,
            SerialNumber: serialNumber,
            Day: body.Day,
            Month: body.Month), ct);

        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Cumpleaños", Detail = result.Error });

        return Ok(new PublicBirthdayResponse(
            result.Value.Day,
            result.Value.Month,
            "Cumpleaños guardado."));
    }

    private async Task<(PublicTenantInfo? Info, IActionResult? Result)> ResolveOperationalTenantAsync(
        string tenantSlug,
        CancellationToken ct)
    {
        var tenant = await _tenantResolver.ResolveBySlugAsync(tenantSlug, ct);
        if (tenant is null)
        {
            return (null, NotFound(new ProblemDetails
            {
                Title = "Programa de lealtad no encontrado.",
                Detail = "Programa de lealtad no encontrado."
            }));
        }

        if (!tenant.IsOperational)
        {
            return (tenant, StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "Programa de lealtad no disponible.",
                Detail = "Este programa de lealtad no está disponible temporalmente."
            }));
        }

        _tenantContext.SetTenant(tenant.TenantId, tenant.Slug);
        return (tenant, null);
    }

    private PublicJoinResponse ToResponse(JoinCustomerResponse value, PublicTenantInfo tenant) =>
        new(
            value.CustomerId,
            value.SerialNumber,
            value.FullName,
            value.Phone,
            value.AlreadyExists,
            BuildMessage(value.AlreadyExists, tenant.DisplayName),
            BuildPassDownloadUrl(value.SerialNumber));

    private static string BuildMessage(bool alreadyExists, string displayName) =>
        alreadyExists
            ? $"Ya tienes una cuenta de {displayName}. Puedes volver a agregar tu tarjeta a Apple Wallet."
            : $"Listo. Tu tarjeta de lealtad {displayName} está lista.";

    private string BuildPassDownloadUrl(string serialNumber)
    {
        var baseUrl = _options.WebServiceURL?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
            baseUrl = $"{Request.Scheme}://{Request.Host}";

        var path = _environment.IsDevelopment()
            ? $"/api/dev/passes/{Uri.EscapeDataString(serialNumber)}"
            : $"/api/passes/{Uri.EscapeDataString(serialNumber)}";

        return $"{baseUrl}{path}";
    }

    public sealed record PublicJoinRequest(
        string FirstName,
        string LastName,
        string Phone);

    public sealed record PublicJoinResponse(
        Guid CustomerId,
        string SerialNumber,
        string FullName,
        string Phone,
        bool AlreadyExists,
        string Message,
        string PassDownloadUrl);

    public sealed record PublicBirthdayRequest(int Day, int Month);

    public sealed record PublicBirthdayResponse(int Day, int Month, string Message);
}
