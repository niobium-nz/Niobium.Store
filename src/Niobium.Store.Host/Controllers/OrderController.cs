using Microsoft.AspNetCore.Mvc;
using Niobium.Platform;
using Niobium.Platform.Captcha.ReCaptcha;
using Niobium.Store.Flows;

namespace Niobium.Store.Host.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class OrderController(OrderFlow flow, IVisitorRiskAssessor assessor) : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> Action(HttpRequest req, OrderRequest request,
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

            var response = await flow.RunAsync(request, req.GetRemoteIP(), cancellationToken);
            return new OkObjectResult(response);
        }
    }
}
