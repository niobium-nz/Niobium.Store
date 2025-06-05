using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Niobium.Store.Functions;

public class Placeholder
{
    [Function(nameof(PaymentWebHook))]
    public IActionResult PaymentWebHook([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = Cod.Platform.Finance.Stripe.Constants.DefaultPaymentWebHookEndpoint)] HttpRequest req) => new OkResult();

    [Function(nameof(PaymentRequest))]
    public IActionResult PaymentRequest([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = Cod.Platform.Finance.Stripe.Constants.DefaultPaymentRequestEndpoint)] HttpRequest req) => new OkResult();
}

