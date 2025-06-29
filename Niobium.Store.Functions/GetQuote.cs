using Cod;
using Cod.Platform;
using Cod.Platform.Captcha.ReCaptcha;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using FromBodyAttribute = Microsoft.Azure.Functions.Worker.Http.FromBodyAttribute;

namespace Niobium.Store.Functions;

public class GetQuote(
    Func<OrderDomain> domainFactory,
    IVisitorRiskAssessor assessor,
    ILogger<GetQuote> logger)
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

        var risk = await req.AssessRiskAsync(assessor, request.ID.ToString(), request.Captcha, logger, cancellationToken);
        if (risk != null)
        {
            return risk;
        }

        var quote = await domainFactory().QuoteAsync(request, cancellationToken);
        return new OkObjectResult(quote);
    }
}