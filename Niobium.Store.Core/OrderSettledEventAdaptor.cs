using Cod;
using Cod.Messaging;

namespace Niobium.Store
{
    internal class OrderSettledEventAdaptor(IMessagingBroker<OrderSettledEvent> queue) : DomainEventHandler<OrderDomain, EntityChangedEvent<Order>>
    {
        public async override Task HandleCoreAsync(EntityChangedEvent<Order> e, CancellationToken cancellationToken)
        {
            if (e.OldEntity != null && e.OldEntity.Paid < e.OldEntity.GrandTotal
                && e.NewEntity != null && e.NewEntity.Paid >= e.NewEntity.GrandTotal)
            {
                await queue.EnqueueAsync(new MessagingEntry<OrderSettledEvent>
                {
                    ID = e.NewEntity.GetFullID(),
                    Value = new OrderSettledEvent { Order = e.NewEntity },
                }, cancellationToken: cancellationToken);
            }
        }
    }
}
