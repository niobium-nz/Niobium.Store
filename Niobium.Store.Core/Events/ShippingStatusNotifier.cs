using Niobium.Messaging;
using Niobium.Notification;

namespace Niobium.Store.Events
{
    internal class ShippingStatusNotifier(IMessagingBroker<NotifyCommand> broker)
        : DomainEventHandler<IDomain<Order>, EntityChangedEventArgs<Order>>
    {
        private const string NotificationNewOrderChannel = "NewOrder";

        protected override DomainEventAudience EventSource => DomainEventAudience.External;

        public override async Task HandleCoreAsync(EntityChangedEventArgs<Order> e, CancellationToken cancellationToken)
        {
            if (e.Entity.Status == (int)OrderStatus.Shipped || e.Entity.Status == (int)OrderStatus.Delivered)
            {
                var id = $"{NotificationNewOrderChannel}-{e.Entity.GetFullID()}";
                await broker.EnqueueAsync(new MessagingEntry<NotifyCommand>
                {
                    ID = id,
                    Value = new NotifyCommand
                    {
                        ID = id,
                        Tenant = e.Entity.Tenant,
                        Channel = NotificationNewOrderChannel,
                        Destination = e.Entity.Email,
                        DestinationDisplayName = e.Entity.Consignee,
                        Parameters = [],
                    },
                }, cancellationToken: cancellationToken);
            }
        }
    }
}
