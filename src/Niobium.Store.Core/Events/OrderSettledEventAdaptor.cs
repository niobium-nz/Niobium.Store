using Niobium.Messaging;

namespace Niobium.Store.Events
{
    internal class OrderSettledEventAdaptor(IMessagingBroker<OrderSettledEvent> queue) : DomainEventHandler<IDomain<Order>, EntityChangedEventArgs<Order>>
    {
        public override async Task HandleCoreAsync(EntityChangedEventArgs<Order> e, CancellationToken cancellationToken)
        {
            if (e.Entity.Status == (int)OrderStatus.Paid)
            {
                await queue.EnqueueAsync(new MessagingEntry<OrderSettledEvent>
                {
                    ID = e.Entity.GetFullID(),
                    Value = new OrderSettledEvent { Order = e.Entity },
                }, cancellationToken: cancellationToken);
            }
        }
    }
}
