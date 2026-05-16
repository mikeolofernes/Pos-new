using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Pos.Application.Common.Abstractions;

namespace Pos.Infrastructure.Identity;

public class JwtOptions
{
    public string Issuer { get; set; } = "pos-api";
    public string Audience { get; set; } = "pos-clients";
    public string SigningKey { get; set; } = default!;       // min 32 chars / 256 bits
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 30;
}

public class JwtService : IJwtService
{
    private readonly JwtOptions _opt;
    private readonly SigningCredentials _creds;

    public JwtService(IOptions<JwtOptions> opt)
    {
        _opt = opt.Value;
        if (_opt.SigningKey.Length < 32)
            throw new InvalidOperationException("JWT signing key must be >= 32 characters");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.SigningKey));
        _creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    public TokenPair Issue(Guid tenantId, Guid userId, string email, string[] permissions, Guid deviceId)
    {
        var now = DateTimeOffset.UtcNow;
        var accessExp = now.AddMinutes(_opt.AccessTokenMinutes);
        var refreshExp = now.AddDays(_opt.RefreshTokenDays);

        var claims = new List<Claim>
        {
            new("tid", tenantId.ToString()),
            new("sub", userId.ToString()),
            new("did", deviceId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };
        claims.AddRange(permissions.Select(p => new Claim("perm", p)));

        var token = new JwtSecurityToken(
            issuer: _opt.Issuer,
            audience: _opt.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: accessExp.UtcDateTime,
            signingCredentials: _creds);

        var access = new JwtSecurityTokenHandler().WriteToken(token);
        var refresh = GenerateRefreshToken();
        return new TokenPair(access, accessExp, refresh, refreshExp);
    }

    public string HashRefresh(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}
