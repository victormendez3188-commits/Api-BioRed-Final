using System.ComponentModel.DataAnnotations;

namespace BioRed.Application.Security;

public sealed class ForgotPasswordRequest
{
    [Required, EmailAddress]
    [RegularExpression(@"(?i)^[^\s@]+@gmail\.com$", ErrorMessage = "Use una dirección @gmail.com.")]
    public string Email { get; init; } = string.Empty;
}
