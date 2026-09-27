namespace BioRed.Application.Security;

public enum TokenRefreshStatus
{
    Success,
    InvalidToken
}

public sealed record TokenRefreshResult(
    TokenRefreshStatus Status,
    TokenRefreshResponse? Response = null);