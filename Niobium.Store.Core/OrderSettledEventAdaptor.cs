using Niobium;
using Niobium.Messaging;

namespace Niobium.Store
{
    internal class OrderSettledEventAdaptor(IMessagingBroker<OrderSettledEvent> queue) : DomainEventHandler<OrderDomain, EntityChangedEventArgs<Order>>
    {
        public async override Task HandleCoreAsync(EntityChangedEventArgs<Order> e, CancellationToken cancellationToken)
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
