using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Niobium.Platform;
using Niobium.Platform.Captcha.ReCaptcha;
using Niobium.Store.Flows;
using FromBodyAttribute = Microsoft.Azure.Functions.Worker.Http.FromBodyAttribute;

namespace Niobium.Store.Functions;

public class TrackOrder(TrackFlow flow, IVisitorRiskAssessor assessor)
{
    [Function(nameof(TrackOrder))]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "track")] HttpRequest req,
        [FromBody] TrackRequest request,
        CancellationToken cancellationToken)
    {
        var valid = request.TryValidate(out var validationState);
        if (!valid || !validationState.IsValid)
        {
            return validationState.MakeResponse();
        }

        var lowRisk = await assessor.AssessAsync(request.Captcha, requestID: request.ID.ToString(), cancellationToken: cancellationToken);
        if (!lowRisk)
        {
            return new UnauthorizedResult();
        }

        var details = await flow.RunAsync(request, cancellationToken);
        return new OkObjectResult(details);
    }
}