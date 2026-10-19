using System.ComponentModel.DataAnnotations;

namespace BioRed.Application.Security;

public sealed record RefreshTokenRequest(
    [Required(
        ErrorMessage = "El Refresh Token es obligatorio.")]
    string RefreshToken);