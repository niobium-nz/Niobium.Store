using Microsoft.AspNetCore.Mvc;
using Niobium.Platform;
using Niobium.Platform.Captcha.ReCaptcha;
using Niobium.Store.Flows;

namespace Niobium.Store.Host.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class OrdersController(OrderFlow flow, IVisitorRiskAssessor assessor) : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> Action(OrderRequest request,
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

            OrderResponse response = await flow.RunAsync(request, this.Request.GetRemoteIP(), cancellationToken);
            return new OkObjectResult(response);
        }
    }
}
