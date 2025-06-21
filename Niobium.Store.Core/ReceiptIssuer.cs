using Cod;
using Microsoft.Extensions.Logging;

namespace Niobium.Store
{
    internal class ReceiptIssuer(ILogger<ReceiptIssuer> logger) : DomainEventHandler<IDomain<Order>, OrderCreatedEvent>
    {
        protected override DomainEventAudience EventSource => DomainEventAudience.External;

        public override Task HandleCoreAsync(OrderCreatedEvent e, CancellationToken cancellationToken)
        {
            logger.LogInformation($"Issuing receipt for order: {e.NewOrder.GetFullID()}");
            return Task.CompletedTask;
        }
    }
}
