using Microsoft.AspNetCore.Authorization;

namespace BioRed.Authorization;

public sealed record PermissionRequirement(
    string Permission) : IAuthorizationRequirement;