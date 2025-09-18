using Niobium.Messaging;

namespace Niobium.Store.Events
{
    internal class FulfillOrderCommandSender(IRepository<QuantifiedListing> repo, IMessagingBroker<FulfillOrderCommand> broker)
        : DomainEventHandler<IDomain<Order>, OrderSettledEvent>
    {
        private const string NewOrderChannel = "NewOrder";

        protected override DomainEventAudience EventSource => DomainEventAudience.External;

        public override async Task HandleCoreAsync(OrderSettledEvent e, CancellationToken cancellationToken)
        {
            List<QuantifiedListing> orderItems = [];
            var items = e.Order.GetCart();
            foreach (var item in items)
            {
                var orderItem = await repo.RetrieveAsync(
                    Listing.BuildPartitionKey(item.Listing),
                    Listing.BuildRowKey(item.Option),
                    cancellationToken: cancellationToken)
                    ?? throw new InvalidOperationException($"Cannot find listing {item.Listing} option {item.Option} for order {e.Order.GetFullID()}");
                orderItem.Quantity = item.Quantity;
                orderItems.Add(orderItem);
            }

            var id = $"{NewOrderChannel}-{e.Order.GetFullID()}";
            await broker.EnqueueAsync(new MessagingEntry<FulfillOrderCommand>
            {
                ID = id,
                Value = new FulfillOrderCommand
                {
                    ID = id,
                    Order = e.Order,
                    Items = orderItems,
                },
            }, cancellationToken: cancellationToken);
        }
    }
}
