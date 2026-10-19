using System.ComponentModel.DataAnnotations;

namespace BioRed.Application.Security;

public sealed class LoginRequest
{
    [Required(ErrorMessage =
        "El nombre de usuario o correo es obligatorio.")]
    [StringLength(
        150,
        MinimumLength = 1,
        ErrorMessage =
            "El nombre de usuario o correo no es válido.")]
    public string UserNameOrEmail { get; init; } = string.Empty;

    [Required(ErrorMessage =
        "La contraseña es obligatoria.")]
    [StringLength(
        200,
        MinimumLength = 1,
        ErrorMessage =
            "La contraseña no es válida.")]
    public string Password { get; init; } = string.Empty;
}