using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi;

namespace BCOBCA.API.Extensions;

public static class OpenApiExtensions
{
    public static IServiceCollection AddBCOBCAOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(options => options.AddDocumentTransformer((document, context, ct) =>
        {
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" };
            var descriptions = context.ApplicationServices.GetRequiredService<IApiDescriptionGroupCollectionProvider>().ApiDescriptionGroups.Items.SelectMany(x => x.Items)
                .Where(x => x.ActionDescriptor.EndpointMetadata.OfType<IAuthorizeData>().Any() && !x.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any()).ToArray();
            foreach (var (path, pathItem) in document.Paths)
            {
                if (pathItem.Operations is null) continue;
                foreach (var (method, operation) in pathItem.Operations)
                {
                    if (!descriptions.Any(x => "/" + x.RelativePath?.Split('?')[0] == path && string.Equals(x.HttpMethod, method.ToString(), StringComparison.OrdinalIgnoreCase))) continue;
                    operation.Security ??= [];
                    operation.Security.Add(new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", document)] = [] });
                }
            }
            return Task.CompletedTask;
        }));
        return services;
    }
}
