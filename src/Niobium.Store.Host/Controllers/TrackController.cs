using Microsoft.AspNetCore.Mvc;
using Niobium.Platform;
using Niobium.Platform.Captcha.ReCaptcha;
using Niobium.Store.Flows;

namespace Niobium.Store.Host.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TrackController(TrackFlow flow, IVisitorRiskAssessor assessor) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> Action(TrackRequest request, CancellationToken cancellationToken)
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
}
