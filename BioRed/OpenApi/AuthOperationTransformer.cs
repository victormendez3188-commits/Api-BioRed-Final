using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;

namespace BioRed.OpenApi;

public sealed class AuthOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var metadata =
            context.Description.ActionDescriptor.EndpointMetadata;

        var requiresAuthorization =
            metadata.OfType<IAuthorizeData>().Any();

        var allowsAnonymous =
            metadata.OfType<IAllowAnonymous>().Any();

        if (!requiresAuthorization || allowsAnonymous)
        {
            return Task.CompletedTask;
        }

        operation.Security ??= new List<OpenApiSecurityRequirement>();

        operation.Security.Add(
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Id = "Bearer",
                        Type = ReferenceType.SecurityScheme
                    }
                }] = Array.Empty<string>()
            });

        return Task.CompletedTask;
    }
}