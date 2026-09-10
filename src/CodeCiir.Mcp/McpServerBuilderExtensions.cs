using CodeCiir.Mcp.Tools;

namespace Microsoft.Extensions.DependencyInjection;

public static class McpServerBuilderExtensions
{
    /// <summary>
    /// Registers the code-ciir-api MCP tools (project discovery, semantic code search + graph,
    /// feedback) on top of whichever transport the host has configured. Tools call straight into
    /// the Application layer - no HTTP round-trip to this API's own REST endpoints.
    /// </summary>
    /// <param name="builder">MCP server builder to register the tools onto.</param>
    public static IMcpServerBuilder AddCodeCiirTools(this IMcpServerBuilder builder)
    {
        builder.WithTools<ProjectTools>();
        builder.WithTools<CodeQueryTools>();
        return builder;
    }
}
