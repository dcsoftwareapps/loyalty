namespace LoyaltyCloud.Application.Common.Interfaces;

public interface IPasswordHashingService
{
    string HashPassword(string password);
    bool VerifyPassword(string passwordHash, string password);
}
