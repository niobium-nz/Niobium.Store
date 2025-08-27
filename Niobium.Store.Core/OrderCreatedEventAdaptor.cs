using Niobium;
using Niobium.Messaging;

namespace Niobium.Store
{
    internal class OrderCreatedEventAdaptor(IMessagingBroker<OrderCreatedEvent> queue) : DomainEventHandler<OrderDomain, EntityChangedEventArgs<Order>>
    {
        public async override Task HandleCoreAsync(EntityChangedEventArgs<Order> e, CancellationToken cancellationToken)
        {
            if (e.ChangeType.HasFlag(EntityChangeType.Created))
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
