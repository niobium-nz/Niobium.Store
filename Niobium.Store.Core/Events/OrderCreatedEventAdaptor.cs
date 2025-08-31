using Niobium.Messaging;

namespace Niobium.Store.Events
{
    internal class OrderCreatedEventAdaptor(IMessagingBroker<OrderCreatedEvent> queue) : DomainEventHandler<IDomain<Order>, EntityChangedEventArgs<Order>>
    {
        public override async Task HandleCoreAsync(EntityChangedEventArgs<Order> e, CancellationToken cancellationToken)
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
