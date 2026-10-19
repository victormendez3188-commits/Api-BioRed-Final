namespace BioRed.Application.Security;

public interface ITokenService
{
    AccessTokenResult CreateAccessToken(
        AuthenticatedUser user);
}