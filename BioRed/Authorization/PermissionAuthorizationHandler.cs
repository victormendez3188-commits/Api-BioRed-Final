using Microsoft.AspNetCore.Authorization;

namespace BioRed.Authorization;

public sealed class PermissionAuthorizationHandler
    : AuthorizationHandler<PermissionRequirement>
{
    private const string PermissionClaimType = "permission";

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        var hasPermission = context.User.Claims.Any(
            claim =>
                string.Equals(
                    claim.Type,
                    PermissionClaimType,
                    StringComparison.Ordinal) &&
                string.Equals(
                    claim.Value,
                    requirement.Permission,
                    StringComparison.OrdinalIgnoreCase));

        if (hasPermission)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}