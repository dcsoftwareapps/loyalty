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

    private readonly CashierRefreshTokenService _refresh;
    private readonly AppDbContext _db;
    private readonly IMutableTenantContext _tenantContext;
    private readonly IPasswordHashingService _passwords;
    private readonly CashierAccessTokenService _tokens;
    private readonly LoyaltyCloud.Common.Services.IDateTimeProvider _clock;
    private readonly ILogger<CashierAuthController> _logger;

    public CashierAuthController(
        CashierRefreshTokenService refresh,
        AppDbContext db,
        IMutableTenantContext tenantContext,
        IPasswordHashingService passwords,
        CashierAccessTokenService tokens,
        LoyaltyCloud.Common.Services.IDateTimeProvider clock,
        ILogger<CashierAuthController> logger)
    {
        _refresh = refresh;
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

        (string Token, DateTimeOffset Expires)? refresh = request.RememberMe ? _refresh.Issue(tenant,user) : null;
        if (refresh is not null) await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Cashier login succeeded. TenantId={TenantId}, UserId={UserId}", tenant.Id, user.Id);
        return Ok(BuildResponse(tenant,user,refresh?.Token,refresh?.Expires));
    }

    [HttpPost("refresh")]
    [EnableRateLimiting(CashierAuthDefaults.LoginRateLimitPolicy)]
    public async Task<IActionResult> Refresh([FromBody] CashierRefreshRequest request,CancellationToken ct)
    {
        var renewed=await _refresh.RotateAsync(request.RefreshToken,ct);
        if (renewed is null) return Unauthorized(InvalidLoginProblem);
        var r=renewed.Value;
        return Ok(BuildResponse(r.Tenant,r.User,r.Token,r.Expires));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] CashierRefreshRequest request,CancellationToken ct)
    {
        await _refresh.RevokeAsync(request.RefreshToken,ct);
        return NoContent();
    }

    private CashierLoginResponse BuildResponse(Tenant tenant,TenantAdminUser user,string? refresh,DateTimeOffset? refreshExpires)
    {
        var token=_tokens.CreateToken(tenant,user);
        return new(token.AccessToken,"Bearer",token.ExpiresAtUtc,
            (int)Math.Max(1,Math.Ceiling((token.ExpiresAtUtc-new DateTimeOffset(DateTime.SpecifyKind(_clock.UtcNow,DateTimeKind.Utc))).TotalSeconds)),
            tenant.Slug,user.Id,user.Username,user.Role.ToString(),tenant.ProgramType.ToString(),refresh,refreshExpires);
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

    public sealed record CashierLoginRequest(string TenantSlug, string Username, string Password, bool RememberMe = false);
    public sealed record CashierRefreshRequest(string RefreshToken);

    public sealed record CashierLoginResponse(
        string AccessToken,
        string TokenType,
        DateTimeOffset ExpiresAtUtc,
        int ExpiresInSeconds,
        string TenantSlug,
        Guid UserId,
        string Username,
        string Role,
        string ProgramType = "Points",
        string? RefreshToken = null, DateTimeOffset? RefreshTokenExpiresAtUtc = null);
}
