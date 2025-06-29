using AutoMapper;
using Cod;
using Cod.Platform;
using Cod.Platform.Captcha.ReCaptcha;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using FromBodyAttribute = Microsoft.Azure.Functions.Worker.Http.FromBodyAttribute;

namespace Niobium.Store.Functions;

public class MakeOrder(
    Func<OrderDomain> domainFactory,
    IVisitorRiskAssessor assessor,
    IMapper mapper,
    ILogger<MakeOrder> logger)
{
    [Function(nameof(MakeOrder))]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "orders")] HttpRequest req,
        [FromBody] OrderRequest request,
        CancellationToken cancellationToken)
    {
        var tenant = req.GetTenant();
        if (string.IsNullOrWhiteSpace(tenant))
        {
            return new BadRequestObjectResult(new { Error = "Tenant is required." });
        }
        request.Tenant = tenant;

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