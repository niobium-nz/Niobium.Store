using Niobium;
using Niobium.Messaging;
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
                ID = e.Order.GetFullID(),
                Value = new SubscribeCommand
                {
                    Email = e.Order.Email,
                    Campaign = e.Order.Items,
                    FirstName = e.Order.Consignee,
                    ID = e.Order.GetID().ToString(),
                    Tenant = e.Order.Tenant,
                }
            }, cancellationToken: cancellationToken);
        }
    }
}
