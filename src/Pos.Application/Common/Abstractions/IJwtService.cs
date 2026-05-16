namespace Pos.Application.Common.Abstractions;

public sealed record TokenPair(string AccessToken, DateTimeOffset AccessExpiresAt,
                               string RefreshToken, DateTimeOffset RefreshExpiresAt);

public interface IJwtService
{
    TokenPair Issue(Guid tenantId, Guid userId, string email, string[] permissions, Guid deviceId);
    string HashRefresh(string token);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}
