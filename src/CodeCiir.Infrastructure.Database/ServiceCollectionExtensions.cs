using CodeCiir.Application.CodeQueries;
using CodeCiir.Application.Feedback;
using CodeCiir.Application.Projects;
using CodeCiir.Infrastructure.Database.CodeQueries;
using CodeCiir.Infrastructure.Database.Feedback;
using CodeCiir.Infrastructure.Database.Projects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CodeCiir.Infrastructure.Database;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Npgsql data source used to read <c>code3rag</c> and the Dapper-backed
    /// repositories built on top of it. This API only reads from that schema - it never runs
    /// migrations against it, since it's owned by code-ciir-indexer (see
    /// .specs/01-schema-discovery.md).
    /// </summary>
    /// <param name="services">Service collection to register into.</param>
    public static IServiceCollection AddDatabaseInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton(sp =>
        {
            // Resolved lazily (on first use) rather than read from the IConfiguration passed in
            // at registration time, so that config sources added after this call - e.g. a test
            // host's in-memory overrides - are still picked up.
            var configuration = sp.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionString("Database")
                ?? throw new InvalidOperationException("Missing required 'ConnectionStrings:Database' configuration value.");

            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
            dataSourceBuilder.UseVector();
            return dataSourceBuilder.Build();
        });

        services.AddScoped<IProjectsRepository, ProjectsRepository>();
        services.AddScoped<ICodeDocumentsRepository, CodeDocumentsRepository>();
        services.AddScoped<IRelationshipGraphRepository, RelationshipGraphRepository>();
        services.AddScoped<IFeedbackRepository, FeedbackRepository>();

        return services;
    }
}
