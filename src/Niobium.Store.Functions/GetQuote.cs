using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Niobium.Platform;
using Niobium.Platform.Captcha.ReCaptcha;
using Niobium.Store.Flows;
using FromBodyAttribute = Microsoft.Azure.Functions.Worker.Http.FromBodyAttribute;

namespace Niobium.Store.Functions;

public class GetQuote(QuoteFlow flow, IVisitorRiskAssessor assessor)
{
    [Function(nameof(GetQuote))]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "quote")] HttpRequest req,
        [FromBody] QuoteRequest request,
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

        var quote = await flow.RunAsync(request, cancellationToken);
        return new OkObjectResult(quote);
    }
}