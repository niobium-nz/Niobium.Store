using Microsoft.AspNetCore.Mvc;
using Niobium.Platform;
using Niobium.Platform.Captcha.ReCaptcha;
using Niobium.Store.Flows;

namespace Niobium.Store.Host.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class QuoteController(QuoteFlow flow, IVisitorRiskAssessor assessor) : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> Action(QuoteRequest request, CancellationToken cancellationToken)
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

            QuoteResponse quote = await flow.RunAsync(request, cancellationToken);
            return new OkObjectResult(quote);
        }
    }
}
