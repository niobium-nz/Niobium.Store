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
    Func<OrderDomain> domainFactory,
    IVisitorRiskAssessor assessor,
    IMapper mapper,
    ILogger<MakeOrder> logger)
{
    private static readonly JsonSerializerOptions serializationOptions = new(JsonSerializerDefaults.Web);

    [Function(nameof(MakeOrder))]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "orders")] HttpRequest req, CancellationToken cancellationToken)
    {
        var request = await JsonSerializer.DeserializeAsync<OrderRequest>(req.Body, options: serializationOptions, cancellationToken: cancellationToken);
        if (request == null)
        {
            return new BadRequestObjectResult(new { Error = "Invalid order request." });
        }

        var tenant = req.GetTenant();
        if (string.IsNullOrWhiteSpace(tenant))
        {
            return new BadRequestObjectResult(new { Error = "Tenant is required." });
        }
        request.Tenant = tenant;

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

        var clientIP = req.GetRemoteIP();
        var domain = domainFactory();
        var order = await domain.TakeNew(request, clientIP, cancellationToken);
        var response = mapper.Map<OrderResponse>(order);
        response.Order = order.GetID();

        var charge = await domain.CreateChargeAsync(request.Tenant, clientIP, cancellationToken);
        if (charge == null || charge.Instruction == null)
        {
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }

        response.Instruction = charge.Instruction.ToString()!;
        return new OkObjectResult(response);
    }
}