using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyCloud.Admin.Services;

public sealed class TenantStaffService
{
    public const int MinimumPasswordLength = 8;

    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPasswordHashingService _passwords;
    private readonly IDateTimeProvider _clock;

    public TenantStaffService(
        AppDbContext db,
        ITenantContext tenantContext,
        IPasswordHashingService passwords,
        IDateTimeProvider clock)
    {
        _db = db;
        _tenantContext = tenantContext;
        _passwords = passwords;
        _clock = clock;
    }

    public async Task<IReadOnlyList<TenantStaffUserDto>> ListAsync(CancellationToken ct = default)
    {
        var tenantId = _tenantContext.RequireTenantId();

        return await _db.TenantAdminUsers
            .AsNoTracking()
            .Where(user => user.TenantId == tenantId)
            .OrderBy(user => user.Role)
            .ThenBy(user => user.Username)
            .Select(user => new TenantStaffUserDto(
                user.Id,
                user.Username,
                user.NormalizedUsername,
                user.Role,
                user.IsActive,
                user.CreatedAt,
                user.LastLoginAt))
            .ToListAsync(ct);
    }

    public async Task<Result<TenantStaffUserDto>> CreateAsync(
        string? username,
        string? password,
        TenantUserRole role,
        CancellationToken ct = default)
    {
        var tenantId = _tenantContext.RequireTenantId();
        var normalized = NormalizeUsernameOrError(username);
        if (normalized.IsFailure)
            return Result.Fail<TenantStaffUserDto>(normalized.Error);

        var passwordValidation = ValidatePassword(password);
        if (passwordValidation.IsFailure)
            return Result.Fail<TenantStaffUserDto>(passwordValidation.Error);

        if (!Enum.IsDefined(role))
            return Result.Fail<TenantStaffUserDto>("Rol invalido.");

        var exists = await _db.TenantAdminUsers
            .AnyAsync(user => user.TenantId == tenantId && user.NormalizedUsername == normalized.Value, ct);
        if (exists)
            return Result.Fail<TenantStaffUserDto>("Ya existe un usuario con ese nombre en este tenant.");

        var adminUser = new TenantAdminUser(
            Guid.NewGuid(),
            tenantId,
            username!.Trim(),
            _passwords.HashPassword(password!),
            _clock.UtcNow,
            isActive: true,
            role);

        _db.TenantAdminUsers.Add(adminUser);
        await _db.SaveChangesAsync(ct);

        return Result.Ok(ToDto(adminUser));
    }

    public async Task<Result> ResetPasswordAsync(
        Guid userId,
        string? newPassword,
        CancellationToken ct = default)
    {
        var passwordValidation = ValidatePassword(newPassword);
        if (passwordValidation.IsFailure)
            return passwordValidation;

        var user = await FindCurrentTenantUserAsync(userId, ct);
        if (user is null)
            return Result.Fail("Usuario no encontrado.");

        user.ChangePasswordHash(_passwords.HashPassword(newPassword!));
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> SetActiveAsync(Guid userId, bool isActive, CancellationToken ct = default)
    {
        var user = await FindCurrentTenantUserAsync(userId, ct);
        if (user is null)
            return Result.Fail("Usuario no encontrado.");

        if (!isActive && user.Role == TenantUserRole.Admin && user.IsActive)
        {
            var tenantId = _tenantContext.RequireTenantId();
            var activeAdmins = await _db.TenantAdminUsers
                .CountAsync(candidate =>
                    candidate.TenantId == tenantId
                    && candidate.Role == TenantUserRole.Admin
                    && candidate.IsActive, ct);
            if (activeAdmins <= 1)
                return Result.Fail("No puedes desactivar el ultimo Admin activo del tenant.");
        }

        if (isActive)
            user.Activate();
        else
            user.Deactivate();

        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    private async Task<TenantAdminUser?> FindCurrentTenantUserAsync(Guid userId, CancellationToken ct)
    {
        var tenantId = _tenantContext.RequireTenantId();
        return await _db.TenantAdminUsers
            .SingleOrDefaultAsync(user => user.TenantId == tenantId && user.Id == userId, ct);
    }

    private static Result<string> NormalizeUsernameOrError(string? username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return Result.Fail<string>("Usuario requerido.");

        try
        {
            return Result.Ok(TenantAdminUser.NormalizeUsername(username.Trim()));
        }
        catch (ArgumentException ex)
        {
            return Result.Fail<string>(ex.Message);
        }
    }

    private static Result ValidatePassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return Result.Fail("Password requerido.");
        if (password.Length < MinimumPasswordLength)
            return Result.Fail($"El password debe tener al menos {MinimumPasswordLength} caracteres.");

        return Result.Ok();
    }

    private static TenantStaffUserDto ToDto(TenantAdminUser user) =>
        new(user.Id, user.Username, user.NormalizedUsername, user.Role, user.IsActive, user.CreatedAt, user.LastLoginAt);
}

public sealed record TenantStaffUserDto(
    Guid Id,
    string Username,
    string NormalizedUsername,
    TenantUserRole Role,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? LastLoginAtUtc);
