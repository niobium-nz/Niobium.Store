using Niobium.Messaging;

namespace Niobium.Store.Events
{
    internal class OrderShippedEventAdaptor(IMessagingBroker<OrderShippedEvent> queue) : DomainEventHandler<IDomain<Order>, EntityChangedEventArgs<Order>>
    {
        public override async Task HandleCoreAsync(EntityChangedEventArgs<Order> e, CancellationToken cancellationToken)
        {
            if (e.Entity.Status == (int)OrderStatus.Shipped)
            {
                await queue.EnqueueAsync(new MessagingEntry<OrderShippedEvent>
                {
                    ID = e.Entity.GetFullID(),
                    Value = new OrderShippedEvent { Order = e.Entity },
                }, cancellationToken: cancellationToken);
            }
        }
    }
}
