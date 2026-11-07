using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Niobium.Store.Server;

/// <summary>Identifies the deployed binary without reading or changing store data.</summary>
public sealed class DeploymentInfo
{
    [Function(nameof(DeploymentInfo))]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequest request)
    {
        string? commit = typeof(DeploymentInfo).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .SingleOrDefault(attribute => attribute.Key == "Commit")?.Value;
        return new OkObjectResult(new { status = "ready", commit });
    }
}
