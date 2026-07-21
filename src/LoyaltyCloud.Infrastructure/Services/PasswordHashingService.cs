using LoyaltyCloud.Application.Common.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class PasswordHashingService : IPasswordHashingService
{
    private readonly PasswordHasher<object> _hasher = new();

    public string HashPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password requerido.", nameof(password));

        return _hasher.HashPassword(new object(), password);
    }

    public bool VerifyPassword(string passwordHash, string password)
    {
        if (string.IsNullOrWhiteSpace(passwordHash) || string.IsNullOrWhiteSpace(password))
            return false;

        var result = _hasher.VerifyHashedPassword(new object(), passwordHash, password);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
