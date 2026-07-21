using System.Security.Claims;
using LoyaltyCloud.Common.Services;
using Microsoft.AspNetCore.Http;

namespace LoyaltyCloud.Infrastructure.Services;

/// <summary>
/// Resuelve la identidad del operador desde <see cref="HttpContext"/>.
/// Para el MVP (auth básica del dueño), <c>UserName</c> sale del claim Name;
/// para futura expansión multi-operador la lógica ya está en el lugar correcto.
/// </summary>
internal sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUserService(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? User => _accessor.HttpContext?.User;

    public string? UserId => User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? User?.Identity?.Name;

    public string? UserName => User?.Identity?.Name;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;
}
