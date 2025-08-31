using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Niobium.Platform;
using Niobium.Platform.Captcha.ReCaptcha;
using Niobium.Store.Flows;
using FromBodyAttribute = Microsoft.Azure.Functions.Worker.Http.FromBodyAttribute;

namespace Niobium.Store.Functions;

public class MakeOrder(OrderFlow flow, IVisitorRiskAssessor assessor)
{
    [Function(nameof(MakeOrder))]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "orders")] HttpRequest req,
        [FromBody] OrderRequest request,
        CancellationToken cancellationToken)
    {
        var tenant = req.GetTenant();
        if (String.IsNullOrWhiteSpace(tenant))
        {
            return new BadRequestObjectResult(new { Error = "Tenant is required." });
        }
        request.Tenant = tenant;

        var valid = request.TryValidate(out var validationState);
        if (!valid || !validationState.IsValid)
        {
            return validationState.MakeResponse();
        }

        var lowRisk = await assessor.AssessAsync(request.Captcha, requestID: request.ID.ToString(), tenant: request.Tenant, cancellationToken: cancellationToken);
        if (!lowRisk)
        {
            return new UnauthorizedResult();
        }

        var clientIP = req.GetRemoteIP();
        var response = await flow.RunAsync(request, clientIP, cancellationToken);
        return new OkObjectResult(response);
    }
}