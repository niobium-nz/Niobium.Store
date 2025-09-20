using Niobium.Messaging;
using Niobium.Notification;

namespace Niobium.Store.Events
{
    internal class SubscriptionSynchronizer(IMessagingBroker<SubscribeCommand> broker) : DomainEventHandler<IDomain<Order>, OrderCreatedEvent>
    {
        protected override DomainEventAudience EventSource => DomainEventAudience.External;

        public override async Task HandleCoreAsync(OrderCreatedEvent e, CancellationToken cancellationToken)
        {
            if (e.Order.MarketingSubscription)
            {
                var campaign = e.Order.GetCart().First().Listing.ToString();
                await broker.EnqueueAsync(new MessagingEntry<SubscribeCommand>
                {
                    ID = e.Order.GetFullID(),
                    Value = new SubscribeCommand
                    {
                        Email = e.Order.Email,
                        Campaign = campaign,
                        FirstName = e.Order.Consignee,
                        ID = e.Order.GetID().ToString(),
                        Tenant = e.Order.Tenant,
                        Track = e.Order.Track,
                    }
                }, cancellationToken: cancellationToken);
            }
        }
    }
}
