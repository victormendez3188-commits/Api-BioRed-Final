using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace BioRed.Authorization;

public sealed class PermissionPolicyProvider
    : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider
        _defaultPolicyProvider;

    public PermissionPolicyProvider(
        IOptions<AuthorizationOptions> options)
    {
        _defaultPolicyProvider =
            new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy?> GetPolicyAsync(
        string policyName)
    {
        if (policyName.StartsWith(
                HasPermissionAttribute.PolicyPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            var permission = policyName[
                HasPermissionAttribute.PolicyPrefix.Length..]
                .Trim();

            if (!string.IsNullOrWhiteSpace(permission))
            {
                var policy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .AddRequirements(
                        new PermissionRequirement(permission))
                    .Build();

                return Task.FromResult<AuthorizationPolicy?>(
                    policy);
            }
        }

        return _defaultPolicyProvider
            .GetPolicyAsync(policyName);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
    {
        return _defaultPolicyProvider
            .GetDefaultPolicyAsync();
    }

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
    {
        return _defaultPolicyProvider
            .GetFallbackPolicyAsync();
    }
}