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
            var amount = new Amount { Cents = entity.Price * quantity, Currency = entity.Currency };
            return new PricedCartItem
            {
                Listing = entity.ID,
                Option = entity.Option,
                Quantity = quantity,
                Unit = new Amount { Cents = entity.Price, Currency = entity.Currency },
                Was = amount,
                Now = amount,
                Tax = new Tax(entity.TaxRate, (TaxKind)entity.TaxKind),
                Discount = Amount.Zero,
                Currency = entity.Currency,
            };
        }
    }
}
