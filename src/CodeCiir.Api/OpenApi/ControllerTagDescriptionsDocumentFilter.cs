using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;
using System.Xml.Linq;

namespace CodeCiir.Api.OpenApi;

/// <summary>
/// Fills in each OpenAPI tag's description from the XML doc summary on the controller class it
/// was grouped by - Swashbuckle reads XML comments for actions/parameters/schemas, but not tags.
/// </summary>
internal sealed class ControllerTagDescriptionsDocumentFilter(string xmlDocPath) : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        var summariesByTypeName = XDocument.Load(xmlDocPath)
            .Descendants("member")
            .Where(member => ((string?)member.Attribute("name"))?.StartsWith("T:", StringComparison.Ordinal) == true)
            .ToDictionary(
                member => ((string)member.Attribute("name")!)[2..],
                member => member.Element("summary")?.Value.Trim());

        var controllerTypesByTag = context.ApiDescriptions
            .Select(description => description.ActionDescriptor as ControllerActionDescriptor)
            .Where(descriptor => descriptor is not null)
            .ToLookup(
                descriptor => descriptor!.ControllerTypeInfo.GetCustomAttribute<ApiExplorerSettingsAttribute>()?.GroupName
                    ?? descriptor.ControllerName,
                descriptor => descriptor!.ControllerTypeInfo);

        foreach (var tag in swaggerDoc.Tags ?? new HashSet<OpenApiTag>())
        {
            var controllerType = controllerTypesByTag[tag.Name!].FirstOrDefault();
            if (controllerType is not null && summariesByTypeName.TryGetValue(controllerType.FullName!, out var summary))
            {
                tag.Description = summary;
            }
        }
    }
}
