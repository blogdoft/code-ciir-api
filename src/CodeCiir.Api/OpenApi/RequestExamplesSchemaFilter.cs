using CodeCiir.Api.Contracts;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Text.Json.Nodes;

namespace CodeCiir.Api.OpenApi;

/// <summary>Attaches a realistic example payload to each request body schema, shown in Swagger UI's "Try it out".</summary>
internal sealed class RequestExamplesSchemaFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema is not OpenApiSchema concrete)
        {
            return;
        }

        if (context.Type == typeof(CodeQueryRequest))
        {
            concrete.Example = new JsonObject
            {
                ["question"] = "Where is the user's password hashed before it is stored?",
                ["projectId"] = 1,
                ["minSimilarity"] = 0.5,
                ["kind"] = "method",
                ["qualifiedName"] = new JsonObject { ["operator"] = "contains", ["value"] = "PasswordHasher" },
                ["limit"] = 10,
            };
        }
        else if (context.Type == typeof(CodeQueryQualifiedNameFilterRequest))
        {
            concrete.Example = new JsonObject { ["operator"] = "contains", ["value"] = "PasswordHasher" };
        }
        else if (context.Type == typeof(CodeQueryFeedbackRequest))
        {
            concrete.Example = new JsonObject
            {
                ["projectId"] = 1,
                ["question"] = "Where is the user's password hashed before it is stored?",
                ["useful"] = true,
                ["similarities"] = new JsonArray(0.91, 0.84, 0.77),
                ["reason"] = "The first match was exactly the method I was looking for.",
                ["user"] = "claude code",
            };
        }
    }
}
