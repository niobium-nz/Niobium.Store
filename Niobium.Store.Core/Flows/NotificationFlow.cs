using Niobium.Messaging;
using Niobium.Notification;
using Niobium.Store.Domains;

namespace Niobium.Store.Flows
{
    internal class NotificationFlow(
        IMessagingBroker<NotifyCommand> broker,
        IDomainRepository<OrderDomain, Order> orderRepo,
        IRepository<Listing> listingRepo)
            : IFlow
    {
        public async Task<NotifyCommand?> RunAsync(Order order, CancellationToken cancellationToken = default)
        {
            var domain = await orderRepo.GetAsync(order, cancellationToken);
            var notification = await domain.GenerateNotificationAsync(cancellationToken);
            if (notification != null)
            {
                var items = new List<Dictionary<string, string>>();
                var cart = order.GetCart();
                foreach (var item in cart)
                {
                    var listing = await listingRepo.RetrieveAsync(
                        Listing.BuildPartitionKey(item.Listing),
                        Listing.BuildRowKey(item.Option),
                        cancellationToken: cancellationToken);
                    if (listing != null)
                    {
                        items.Add(listing.BuildNotificationParameters(item.Quantity).ToDictionary());
                    }
                }

                notification.Parameters["ITEMS"] = items;

                await broker.EnqueueAsync(new MessagingEntry<NotifyCommand>
                {
                    ID = order.GetFullID(),
                    Value = notification,
                }, cancellationToken: cancellationToken);
            }

            return notification;
        }
    }
}
