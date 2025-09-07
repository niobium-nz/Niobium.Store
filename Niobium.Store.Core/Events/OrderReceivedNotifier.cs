using Niobium.Messaging;
using Niobium.Notification;

namespace Niobium.Store.Events
{
    internal class OrderReceivedNotifier(IMessagingBroker<NotifyCommand> broker)
        : DomainEventHandler<IDomain<Order>, OrderSettledEvent>
    {
        private const string NotificationNewOrderChannel = "NewOrder";

        protected override DomainEventAudience EventSource => DomainEventAudience.External;

        public override async Task HandleCoreAsync(OrderSettledEvent e, CancellationToken cancellationToken)
        {
            var id = $"{NotificationNewOrderChannel}-{e.Order.GetFullID()}";
            await broker.EnqueueAsync(new MessagingEntry<NotifyCommand>
            {
                ID = id,
                Value = new NotifyCommand
                {
                    ID = id,
                    Tenant = e.Order.Tenant,
                    Channel = NotificationNewOrderChannel,
                    Destination = e.Order.Email,
                    DestinationDisplayName = e.Order.Consignee,
                    Parameters = [],
                },
            }, cancellationToken: cancellationToken);
        }
    }
}
