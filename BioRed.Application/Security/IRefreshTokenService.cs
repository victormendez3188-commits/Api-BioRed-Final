namespace BioRed.Application.Security;

public interface IRefreshTokenService
{
    RefreshTokenResult CreateRefreshToken();

    string HashToken(string token);
}