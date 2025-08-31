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

        public async Task<TaxableAmount> QuoteAsync(int quantity, CancellationToken cancellationToken)
        {
            var entity = await this.GetEntityAsync(cancellationToken);
            return new TaxableAmount
            {
                Amount = new Amount { Cents = entity.Price * quantity, Currency = entity.Currency },
                Tax = new Tax(entity.TaxRate, (TaxKind)entity.TaxKind),
            };
        }
    }
}
