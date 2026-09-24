using CodeCiir.Api.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Shouldly;
using Xunit;

namespace CodeCiir.Api.Tests.Extensions;

public sealed class ObservabilityExtensionsTests
{
    [Fact]
    public void Should_ReportTheDefaultServiceName_When_OtelServiceNameIsNotSet()
    {
        var serviceName = ResolveServiceName(otelServiceName: null);

        serviceName.ShouldBe(ObservabilityExtensions.DefaultServiceName);
    }

    [Fact]
    public void Should_ReportTheServiceNameFromConfiguration_When_OtelServiceNameIsSet()
    {
        var serviceName = ResolveServiceName(otelServiceName: "custom-service");

        serviceName.ShouldBe("custom-service");
    }

    [Fact]
    public void Should_NotReportTheAssemblyName_When_TracingIsEnabled()
    {
        var serviceName = ResolveServiceName(otelServiceName: null);

        serviceName.ShouldNotBe("CodeCiir.Api");
    }

    private static string ResolveServiceName(string? otelServiceName)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Observability:OpenTelemetry:UseTracingExporter"] = "Otlp",
            ["Observability:OtlpExporterOptions:Endpoint"] = "http://localhost:4317",
            ["OTEL_SERVICE_NAME"] = otelServiceName,
        });
        builder.AddObservability();

        using var provider = builder.Services.BuildServiceProvider();
        var attributes = provider.GetRequiredService<TracerProvider>().GetResource().Attributes;

        return attributes.Single(attribute => attribute.Key == "service.name").Value.ToString()!;
    }
}
