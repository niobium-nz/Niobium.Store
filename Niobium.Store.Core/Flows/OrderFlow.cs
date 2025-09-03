using Microsoft.Extensions.Logging;
using Niobium.Platform.Finance;
using Niobium.Store.Domains;

namespace Niobium.Store.Flows
{
    public class OrderFlow(
        Func<OrderDomain> domainFactory,
        Lazy<IPaymentService> paymentService,
        QuoteFlow quoteFlow,
        ILogger<OrderFlow> logger) : IFlow
    {
        public async Task<OrderResponse> RunAsync(OrderRequest request, string? clientIP, CancellationToken cancellationToken)
        {
            var quote = await quoteFlow.RunAsync(request, cancellationToken);

            var domain = domainFactory();
            var order = await domain.TakeNew(request, quote, clientIP, cancellationToken);
            var response = OrderResponse.Map(order);

            var chargeRequest = await domain.CreateChargeAsync(clientIP, cancellationToken);
            var chargeResult = await paymentService.Value.ChargeAsync(chargeRequest);
            if (chargeResult == null || !chargeResult.IsSuccess || chargeResult.Result?.Instruction == null)
            {
                logger.LogError($"Failed to process charge against order {order.GetFullID()}: {chargeResult?.Message}");
                throw new ApplicationException(InternalError.InternalServerError, "Failed to create payment instruction.");
            }

            response.Instruction = chargeResult.Result.Instruction.ToString()!;
            return response;
        }
    }
}
