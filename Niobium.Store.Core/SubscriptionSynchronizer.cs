using Cod;
using Cod.Messaging;
using Niobium.Notification;

namespace Niobium.Store
{
    internal class SubscriptionSynchronizer(IMessagingBroker<SubscribeCommand> broker) : DomainEventHandler<OrderDomain, OrderCreatedEvent>
    {
        protected override DomainEventAudience EventSource => DomainEventAudience.External;

        public async override Task HandleCoreAsync(OrderCreatedEvent e, CancellationToken cancellationToken)
        {
            await broker.EnqueueAsync(new MessagingEntry<SubscribeCommand>
            {
                ID = e.NewOrder.GetFullID(),
                Value = new SubscribeCommand
                {
                    Email = e.NewOrder.Email,
                    Campaign = e.NewOrder.Items,
                    FirstName = e.NewOrder.Consignee,
                    ID = e.NewOrder.GetID().ToString(),
                    Tenant = e.NewOrder.Tenant,
                }
            }, cancellationToken: cancellationToken);
        }
    }
}
