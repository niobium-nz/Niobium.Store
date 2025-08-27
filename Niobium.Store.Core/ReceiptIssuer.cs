using Niobium;
using Microsoft.Extensions.Logging;

namespace Niobium.Store
{
    internal class ReceiptIssuer(ILogger<ReceiptIssuer> logger) : DomainEventHandler<OrderDomain, OrderSettledEvent>
    {
        protected override DomainEventAudience EventSource => DomainEventAudience.External;

        public override Task HandleCoreAsync(OrderSettledEvent e, CancellationToken cancellationToken)
        {
            logger.LogInformation($"Issuing receipt for order: {e.Order.GetFullID()}");
            return Task.CompletedTask;
        }
    }
}
