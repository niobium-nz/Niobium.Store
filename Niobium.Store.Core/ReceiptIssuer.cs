using Cod;
using Microsoft.Extensions.Logging;

namespace Niobium.Store
{
    internal class ReceiptIssuer(ILogger<ReceiptIssuer> logger) : DomainEventHandler<IDomain<Order>, OrderUpdatedEvent>
    {
        protected override DomainEventAudience EventSource => DomainEventAudience.External;

        public override Task HandleCoreAsync(OrderUpdatedEvent e, CancellationToken cancellationToken)
        {
            logger.LogInformation($"Issuing receipt for order: {e.Order.GetFullID()}");
            return Task.CompletedTask;
        }
    }
}
