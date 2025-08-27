using Niobium;
using Niobium.Platform;
using Niobium.Platform.Captcha.ReCaptcha;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using FromBodyAttribute = Microsoft.Azure.Functions.Worker.Http.FromBodyAttribute;

namespace Niobium.Store.Functions;

public class GetQuote(
    Func<OrderDomain> domainFactory,
    IVisitorRiskAssessor assessor)
{
    [Function(nameof(GetQuote))]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "quote")] HttpRequest req,
        [FromBody] QuoteRequest request,
        CancellationToken cancellationToken)
    {
        request.TryValidate(out var validationState);
        if (!validationState.IsValid)
        {
            return validationState.MakeResponse();
        }

        await assessor.AssessAsync(request.Captcha, requestID: request.ID.ToString(), cancellationToken: cancellationToken);

        var quote = await domainFactory().QuoteAsync(request, cancellationToken);
        return new OkObjectResult(quote);
    }
}