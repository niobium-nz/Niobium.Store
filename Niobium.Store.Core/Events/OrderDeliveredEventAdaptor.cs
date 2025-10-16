using Niobium.Messaging;

namespace Niobium.Store.Events
{
    internal class OrderDeliveredEventAdaptor(IMessagingBroker<OrderDeliveredEvent> queue) : DomainEventHandler<IDomain<Order>, EntityChangedEventArgs<Order>>
    {
        public override async Task HandleCoreAsync(EntityChangedEventArgs<Order> e, CancellationToken cancellationToken)
        {
            if (e.Entity.Status == (int)OrderStatus.Delivered)
            {
                await queue.EnqueueAsync(new MessagingEntry<OrderDeliveredEvent>
                {
                    ID = e.Entity.GetFullID(),
                    Value = new OrderDeliveredEvent { Order = e.Entity },
                }, cancellationToken: cancellationToken);
            }
        }
    }
}
