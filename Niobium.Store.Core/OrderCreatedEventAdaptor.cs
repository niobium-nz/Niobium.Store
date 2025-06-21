using Cod;
using Cod.Messaging;

namespace Niobium.Store
{
    internal class OrderCreatedEventAdaptor(IMessagingBroker<OrderCreatedEvent> queue) : DomainEventHandler<OrderDomain, EntityChangedEvent<Order>>
    {
        public async override Task HandleCoreAsync(EntityChangedEvent<Order> e, CancellationToken cancellationToken)
        {
            await queue.EnqueueAsync(new MessagingEntry<OrderCreatedEvent>
            {
                ID = e.NewEntity.GetFullID(),
                Value = new OrderCreatedEvent { NewOrder = e.NewEntity },
            }, cancellationToken: cancellationToken);
        }
    }
}
