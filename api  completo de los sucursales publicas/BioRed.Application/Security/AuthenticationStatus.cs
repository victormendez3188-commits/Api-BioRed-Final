namespace BioRed.Application.Security;

public enum AuthenticationStatus
{
    Success = 1,
    InvalidCredentials = 2,
    InactiveUser = 3,
    LockedOut = 4,
    PasswordChangeRequired = 5,
    TwoFactorRequired = 6,
    TemporaryPasswordExpired = 7
}
