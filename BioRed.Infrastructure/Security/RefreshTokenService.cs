using System.Security.Cryptography;
using System.Text;
using BioRed.Application.Security;
using Microsoft.IdentityModel.Tokens;

namespace BioRed.Infrastructure.Security;

public sealed class RefreshTokenService : IRefreshTokenService
{
    private const int TokenSizeInBytes = 64;

    private readonly int _refreshTokenDays;

    public RefreshTokenService(int refreshTokenDays)
    {
        if (refreshTokenDays <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(refreshTokenDays),
                "La duración del Refresh Token debe ser mayor que cero.");
        }

        _refreshTokenDays = refreshTokenDays;
    }

    public RefreshTokenResult CreateRefreshToken()
    {
        var createdAtUtc = DateTime.UtcNow;
        var expiresAtUtc =
            createdAtUtc.AddDays(_refreshTokenDays);

        var randomBytes =
            RandomNumberGenerator.GetBytes(TokenSizeInBytes);

        var token =
            Base64UrlEncoder.Encode(randomBytes);

        var tokenHash =
            HashToken(token);

        return new RefreshTokenResult(
            token,
            tokenHash,
            createdAtUtc,
            expiresAtUtc);
    }

    public string HashToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException(
                "El Refresh Token es obligatorio.",
                nameof(token));
        }

        var tokenBytes =
            Encoding.UTF8.GetBytes(token);

        var hashBytes =
            SHA256.HashData(tokenBytes);

        return Convert.ToHexString(hashBytes);
    }
}