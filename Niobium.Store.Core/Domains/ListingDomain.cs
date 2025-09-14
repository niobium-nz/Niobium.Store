using Niobium.Finance;
using Niobium.Invoicing;

namespace Niobium.Store.Domains
{
    public class ListingDomain(
        Lazy<IRepository<Listing>> repository,
        IEnumerable<IDomainEventHandler<IDomain<Listing>>> eventHandlers)
        : GenericDomain<Listing>(repository, eventHandlers)
    {
        private const string InvoiceItemSubject = "Online Order";

        public async Task<InvoiceItem> BuildInvoiceItemAsync(long invoiceID, int sequence, int quantity, CancellationToken cancellationToken = default)
        {
            if (quantity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(quantity), "Input must be greater than zero.");
            }

            var entity = await this.GetEntityAsync(cancellationToken);
            return new InvoiceItem
            {
                ID = invoiceID + sequence,
                Invoice = Invoice.ParseID(invoiceID),
                Subject = InvoiceItemSubject,
                Description = entity.Name,
                Quantity = quantity,
                UnitPriceCents = entity.Price,
                UnitPriceCurrency = entity.Currency,
                LineTotalCents = entity.Price * quantity,
                LineTotalCurrency = entity.Currency
            };
        }

        public async Task<PricedCartItem> QuoteAsync(int quantity, CancellationToken cancellationToken)
        {
            var entity = await this.GetEntityAsync(cancellationToken);
            var amount = entity.Price * quantity;
            return new PricedCartItem
            {
                Listing = entity.ID,
                Option = entity.Option,
                Quantity = quantity,
                Unit = entity.Price,
                Was = amount,
                Now = amount,
                Tax = new Tax(entity.TaxRate, (TaxKind)entity.TaxKind),
                Discount = 0,
                Currency = entity.Currency,
            };
        }
    }
}
