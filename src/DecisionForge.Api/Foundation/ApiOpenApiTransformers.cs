using System.Text.Json.Nodes;
using DecisionForge.Api.Identity;
using DecisionForge.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace DecisionForge.Api.Foundation;

public sealed class ApiOpenApiDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        document.Info = new OpenApiInfo
        {
            Title = "DecisionForge API",
            Version = "v1",
            Description = "Secure same-origin API for explainable procurement decisions.",
        };
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??=
            new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal);
        document.Components.SecuritySchemes["cookieAuth"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Cookie,
            Name = DecisionForgeIdentityDefaults.AuthenticationCookieName,
            Description = "HTTP-only secure same-origin application cookie.",
        };
        return Task.CompletedTask;
    }
}

public sealed class ApiOpenApiSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context.JsonTypeInfo.Type == typeof(LoginRequest))
        {
            schema.Example = JsonNode.Parse(
                """
                {"email":"requester@decisionforge.local","password":"configured-demo-password"}
                """);
        }
        else if (context.JsonTypeInfo.Type == typeof(ApiProblemDetails))
        {
            schema.Example = JsonNode.Parse(
                """
                {"type":"https://decisionforge.local/problems/validation.field","title":"The request contains invalid fields.","status":400,"errorCode":"validation.field","traceId":"00-example-trace-00","instance":"/api/v1/example"}
                """);
        }

        return Task.CompletedTask;
    }
}

public sealed class ApiOpenApiOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IList<object> metadata = context.Description.ActionDescriptor.EndpointMetadata;
        bool isAnonymous = metadata.OfType<IAllowAnonymous>().Any();
        bool requiresAuthorization = metadata.OfType<IAuthorizeData>().Any();
        if (!isAnonymous && requiresAuthorization)
        {
            operation.Security ??= [];
            OpenApiSecurityRequirement requirement = new()
            {
                [new OpenApiSecuritySchemeReference(
                    "cookieAuth",
                    context.Document,
                    externalResource: null)] = [],
            };
            operation.Security.Add(requirement);
        }

        return Task.CompletedTask;
    }
}
