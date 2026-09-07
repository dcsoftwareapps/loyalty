using LoyaltyCloud.API.Auth;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyCloud.API.Controllers;

[ApiController]
[Route("api/auth/cashier")]
[Produces("application/json")]
public sealed class CashierAuthController : ControllerBase
{
    private static readonly ProblemDetails InvalidLoginProblem = new()
    {
        Title = "Autenticacion",
        Detail = "Credenciales invalidas."
    };

    private readonly AppDbContext _db;
    private readonly IMutableTenantContext _tenantContext;
    private readonly IPasswordHashingService _passwords;
    private readonly CashierAccessTokenService _tokens;
    private readonly LoyaltyCloud.Common.Services.IDateTimeProvider _clock;
    private readonly ILogger<CashierAuthController> _logger;

    public CashierAuthController(
        AppDbContext db,
        IMutableTenantContext tenantContext,
        IPasswordHashingService passwords,
        CashierAccessTokenService tokens,
        LoyaltyCloud.Common.Services.IDateTimeProvider clock,
        ILogger<CashierAuthController> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _passwords = passwords;
        _tokens = tokens;
        _clock = clock;
        _logger = logger;
    }

    [HttpPost("login")]
    [EnableRateLimiting(CashierAuthDefaults.LoginRateLimitPolicy)]
    [ProducesResponseType(typeof(CashierLoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] CashierLoginRequest request, CancellationToken ct)
    {
        var tenant = await ResolveTenantAsync(request.TenantSlug, ct);
        if (tenant is null || !tenant.IsActive || tenant.Subscription is null || !tenant.Subscription.IsOperational(_clock.UtcNow))
        {
            _logger.LogWarning("Cashier login failed. TenantSlug={TenantSlug}, Reason={Reason}", request.TenantSlug, "tenant_unavailable");
            return Unauthorized(InvalidLoginProblem);
        }

        _tenantContext.SetTenant(tenant.Id, tenant.Slug);
        var user = string.IsNullOrWhiteSpace(request.Username)
            ? null
            : await _db.TenantAdminUsers.SingleOrDefaultAsync(
                u => u.TenantId == tenant.Id && u.NormalizedUsername == TenantAdminUser.NormalizeUsername(request.Username.Trim()),
                ct);

        if (user is null
            || !user.IsActive
            || !_passwords.VerifyPassword(user.PasswordHash, request.Password))
        {
            _logger.LogWarning("Cashier login failed. TenantSlug={TenantSlug}, Reason={Reason}", tenant.Slug, "invalid_credentials");
            return Unauthorized(InvalidLoginProblem);
        }

        var issuedAt = new DateTimeOffset(DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc));
        var token = _tokens.CreateToken(tenant, user);
        _logger.LogInformation(
            "Cashier login succeeded. TenantId={TenantId}, TenantSlug={TenantSlug}, UserId={UserId}, Role={Role}.",
            tenant.Id,
            tenant.Slug,
            user.Id,
            user.Role);

        return Ok(new CashierLoginResponse(
            token.AccessToken,
            "Bearer",
            token.ExpiresAtUtc,
            (int)Math.Max(1, Math.Ceiling((token.ExpiresAtUtc - issuedAt).TotalSeconds)),
            tenant.Slug,
            user.Id,
            user.Username,
            user.Role.ToString()));
    }

    private async Task<Tenant?> ResolveTenantAsync(string tenantSlug, CancellationToken ct)
    {
        try
        {
            var normalizedSlug = Tenant.NormalizeSlug(tenantSlug);
            return await _db.Tenants
                .AsNoTracking()
                .Include(t => t.Subscription)
                .SingleOrDefaultAsync(t => t.Slug == normalizedSlug, ct);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    public sealed record CashierLoginRequest(string TenantSlug, string Username, string Password);

    public sealed record CashierLoginResponse(
        string AccessToken,
        string TokenType,
        DateTimeOffset ExpiresAtUtc,
        int ExpiresInSeconds,
        string TenantSlug,
        Guid UserId,
        string Username,
        string Role);
}
