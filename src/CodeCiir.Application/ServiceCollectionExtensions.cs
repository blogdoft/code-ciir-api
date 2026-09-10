using CodeCiir.Application.CodeQueries;
using CodeCiir.Application.Feedback;
using CodeCiir.Application.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace CodeCiir.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IProjectsService, ProjectsService>();
        services.AddScoped<ICodeQueryService, CodeQueryService>();
        services.AddScoped<IFeedbackService, FeedbackService>();
        return services;
    }
}
