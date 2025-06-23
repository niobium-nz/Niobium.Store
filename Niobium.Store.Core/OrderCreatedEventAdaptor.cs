using Cod;
using Cod.Messaging;

namespace Niobium.Store
{
    internal class OrderCreatedEventAdaptor(IMessagingBroker<OrderCreatedEvent> queue) : DomainEventHandler<OrderDomain, EntityChangedEvent<Order>>
    {
        public async override Task HandleCoreAsync(EntityChangedEvent<Order> e, CancellationToken cancellationToken)
        {
            if (e.ChangeType.HasFlag(EntityChangeType.Created) && e.Entity != null)
            {
                await queue.EnqueueAsync(new MessagingEntry<OrderCreatedEvent>
                {
                    ID = e.Entity.GetFullID(),
                    Value = new OrderCreatedEvent { Order = e.Entity },
                }, cancellationToken: cancellationToken);
            }
        }
    }
}
