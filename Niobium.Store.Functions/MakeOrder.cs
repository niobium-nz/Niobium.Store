using System.Text.Json;
using AutoMapper;
using Cod;
using Cod.Platform;
using Cod.Platform.Captcha.ReCaptcha;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Niobium.Store.Functions;

public class MakeOrder(
    Func<OrderDomain> orderFactory,
    IVisitorRiskAssessor assessor,
    IMapper mapper,
    ILogger<MakeOrder> logger)
{
    private static readonly JsonSerializerOptions serializationOptions = new(JsonSerializerDefaults.Web);

    [Function(nameof(MakeOrder))]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "orders")] HttpRequest req, CancellationToken cancellationToken)
    {
        var referer = req.Headers.Referer.SingleOrDefault();
        Uri? refererUri = null;
        if (referer == null || !Uri.TryCreate(referer, UriKind.Absolute, out refererUri))
        {
#if !DEBUG
            return new BadRequestResult();
#endif
        }

        var tenant = refererUri?.Host.ToLowerInvariant();

        var request = await JsonSerializer.DeserializeAsync<OrderRequest>(req.Body, options: serializationOptions, cancellationToken: cancellationToken);
        ArgumentNullException.ThrowIfNull(request);

        request.TryValidate(out var validationState);

        if (!validationState.IsValid)
        {
            logger.LogWarning("Validation failed for order request: {Errors}", JsonSerializer.Serialize(validationState.ToDictionary(), serializationOptions));
            return validationState.MakeResponse();
        }

        var clientIP = req.GetRemoteIP();
        var lowRisk = await assessor.AssessAsync(request.ID, tenant!, request.Captcha, clientIP, cancellationToken);
        if (!lowRisk)
        {
            logger.LogWarning($"{clientIP} is considered high risk for order: {request.ID}");
            return new ForbidResult();
        }

        var order = await orderFactory().TakeNew(request, clientIP, cancellationToken);
        var response = mapper.Map<OrderResponse>(order);
        response.Order = order.GetID();
        return new OkObjectResult(response);
    }
}