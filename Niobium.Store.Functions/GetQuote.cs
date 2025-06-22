using System.Text.Json;
using Cod;
using Cod.Platform;
using Cod.Platform.Captcha.ReCaptcha;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Niobium.Store.Functions;

public class GetQuote(
    Func<OrderDomain> domainFactory,
    IVisitorRiskAssessor assessor,
    ILogger<GetQuote> logger)
{
    private static readonly JsonSerializerOptions serializationOptions = new(JsonSerializerDefaults.Web);

    [Function(nameof(GetQuote))]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "quote")] HttpRequest req, CancellationToken cancellationToken)
    {
        var request = await JsonSerializer.DeserializeAsync<QuoteRequest>(req.Body, options: serializationOptions, cancellationToken: cancellationToken);
        if (request == null)
        {
            return new BadRequestResult();
        }

        request.TryValidate(out var validationState);
        if (!validationState.IsValid)
        {
            logger.LogWarning("Validation failed for order request: {Errors}", JsonSerializer.Serialize(validationState.ToDictionary(), serializationOptions));
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