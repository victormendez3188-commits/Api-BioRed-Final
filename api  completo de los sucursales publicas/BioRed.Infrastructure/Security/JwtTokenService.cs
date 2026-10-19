using System.Globalization;
using System.Security.Claims;
using BioRed.Application.Security;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace BioRed.Infrastructure.Security;

public sealed class JwtTokenService : ITokenService
{
    private const string PermissionClaimType = "permission";

    private readonly JsonWebTokenHandler _tokenHandler = new();
    private readonly SigningCredentials _signingCredentials;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _accessTokenMinutes;

    public JwtTokenService(
        byte[] signingKey,
        string issuer,
        string audience,
        int accessTokenMinutes)
    {
        ArgumentNullException.ThrowIfNull(signingKey);

        if (signingKey.Length < 32)
        {
            throw new ArgumentException(
                "La clave JWT debe tener como mínimo 32 bytes.",
                nameof(signingKey));
        }

        if (string.IsNullOrWhiteSpace(issuer))
        {
            throw new ArgumentException(
                "El emisor JWT es obligatorio.",
                nameof(issuer));
        }

        if (string.IsNullOrWhiteSpace(audience))
        {
            throw new ArgumentException(
                "La audiencia JWT es obligatoria.",
                nameof(audience));
        }

        if (accessTokenMinutes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(accessTokenMinutes),
                "La duración del token debe ser mayor que cero.");
        }

        _issuer = issuer;
        _audience = audience;
        _accessTokenMinutes = accessTokenMinutes;

        var securityKey =
            new SymmetricSecurityKey(signingKey.ToArray());

        _signingCredentials = new SigningCredentials(
            securityKey,
            SecurityAlgorithms.HmacSha256);
    }

    public AccessTokenResult CreateAccessToken(
        AuthenticatedUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var issuedAtUtc = DateTime.UtcNow;
        var expiresAtUtc =
            issuedAtUtc.AddMinutes(_accessTokenMinutes);

        var claims = new List<Claim>
        {
            new(
                JwtRegisteredClaimNames.Sub,
                user.UserId.ToString(CultureInfo.InvariantCulture)),

            new(
                ClaimTypes.NameIdentifier,
                user.UserId.ToString(CultureInfo.InvariantCulture)),

            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.Email, user.Email),
            new("full_name", user.FullName),
            new("user_type", user.UserType),
            new("reference_id", user.ReferenceId),

            new(
                JwtRegisteredClaimNames.Jti,
                Guid.NewGuid().ToString("N"))
        };

        claims.AddRange(
            user.Roles
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(role => new Claim(ClaimTypes.Role, role)));

        claims.AddRange(
            user.Permissions
                .Where(permission =>
                    !string.IsNullOrWhiteSpace(permission))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(permission =>
                    new Claim(PermissionClaimType, permission)));

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _issuer,
            Audience = _audience,
            IssuedAt = issuedAtUtc,
            NotBefore = issuedAtUtc,
            Expires = expiresAtUtc,
            SigningCredentials = _signingCredentials
        };

        var token = _tokenHandler.CreateToken(tokenDescriptor);

        return new AccessTokenResult(
            token,
            expiresAtUtc);
    }
}
