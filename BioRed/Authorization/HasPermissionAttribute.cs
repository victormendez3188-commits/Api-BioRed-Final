using Microsoft.AspNetCore.Authorization;

namespace BioRed.Authorization;

[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Method,
    AllowMultiple = true,
    Inherited = true)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "Permission:";

    public HasPermissionAttribute(string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);

        Policy =
            $"{PolicyPrefix}{permission.Trim().ToUpperInvariant()}";
    }
}