using Niobium.Invoicing;
using Niobium.Messaging;
using Niobium.Store.Domains;

namespace Niobium.Store.Flows
{
    internal class InvoiceFlow(
        IMessagingBroker<IssueInvoiceCommand> broker,
        IDomainRepository<OrderDomain, Order> orderRepo,
        IDomainRepository<ListingDomain, Listing> listingRepo)
        : IFlow
    {
        public async Task RunAsync(Order order, CancellationToken cancellationToken = default)
        {
            var domain = await orderRepo.GetAsync(order, cancellationToken);
            var invoice = await domain.IssueInvoiceAsync(cancellationToken);

            var cart = order.GetCart();
            var i = 0;
            foreach (var item in cart)
            {
                var listing = await listingRepo.GetAsync(
                    Listing.BuildPartitionKey(item.Listing),
                    Listing.BuildRowKey(item.Option),
                    cancellationToken: cancellationToken);
                var invoiceItem = await listing.BuildInvoiceItemAsync(invoice.InvoiceID, i, item.Quantity, cancellationToken);
                invoice.InvoiceItems.Add(invoiceItem);
                i++;
            }

            await broker.EnqueueAsync(new MessagingEntry<IssueInvoiceCommand>
            {
                ID = order.GetFullID(),
                Value = invoice,
            }, cancellationToken: cancellationToken);
        }
    }
}
