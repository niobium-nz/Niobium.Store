using Cod;
using Microsoft.Extensions.Logging;

namespace Niobium.Store
{
    internal class ReceiptIssuer(ILogger<ReceiptIssuer> logger) : DomainEventHandler<IDomain<Order>, EntityChangedEvent<Order>>
    {
        protected override DomainEventAudience EventSource => DomainEventAudience.External;

        public override Task HandleCoreAsync(EntityChangedEvent<Order> e, CancellationToken cancellationToken)
        {
            if (e.OldEntity == null && e.NewEntity != null)
            {
                logger.LogInformation($"Issuing receipt for order: {e.NewEntity.GetFullID()}");
            }
            return Task.CompletedTask;
        }
    }
}
