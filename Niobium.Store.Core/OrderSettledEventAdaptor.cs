using Cod;
using Cod.Messaging;

namespace Niobium.Store
{
    internal class OrderSettledEventAdaptor(IMessagingBroker<OrderSettledEvent> queue) : DomainEventHandler<OrderDomain, EntityChangedEvent<Order>>
    {
        public async override Task HandleCoreAsync(EntityChangedEvent<Order> e, CancellationToken cancellationToken)
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
