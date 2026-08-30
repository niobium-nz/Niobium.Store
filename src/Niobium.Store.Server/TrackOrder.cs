using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Niobium.Platform;
using Niobium.Platform.Captcha.ReCaptcha;
using Niobium.Store.Flows;
using FromBodyAttribute = Microsoft.Azure.Functions.Worker.Http.FromBodyAttribute;

namespace Niobium.Store.Server;

public class TrackOrder(TrackFlow flow, IVisitorRiskAssessor assessor)
{
    [Function(nameof(TrackOrder))]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "track")] HttpRequest req,
        [FromBody] TrackRequest request,
        CancellationToken cancellationToken)
    {
        bool valid = request.TryValidate(out ValidationState? validationState);
        if (!valid || !validationState.IsValid)
        {
            return validationState.MakeResponse();
        }

        bool lowRisk = await assessor.AssessAsync(request.Captcha, requestID: request.ID.ToString(), cancellationToken: cancellationToken);
        if (!lowRisk)
        {
            return new UnauthorizedResult();
        }

        TrackResponse details = await flow.RunAsync(request, cancellationToken);
        return new OkObjectResult(details);
    }
}