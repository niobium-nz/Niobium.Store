using Niobium.Finance;
using Niobium.Invoicing;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Niobium.Store.Domains
{
    public class ListingDomain(
        Lazy<IRepository<Listing>> repository,
        IEnumerable<IDomainEventHandler<IDomain<Listing>>> eventHandlers)
        : GenericDomain<Listing>(repository, eventHandlers)
    {
        private const string InvoiceItemSubject = "Online Order";

        public async Task<InvoiceItem> BuildInvoiceItemAsync(long invoiceID, int quantity, CancellationToken cancellationToken = default)
        {
            if (quantity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(quantity), "Input must be greater than zero.");
            }

            var entity = await this.GetEntityAsync(cancellationToken);
            return new InvoiceItem
            {
                ID = invoiceID + entity.ID,
                Invoice = Invoice.ParseID(invoiceID),
                Subject = InvoiceItemSubject,
                Description = entity.Name,
                Quantity = quantity,
                UnitPriceCents = (entity.Price * 10000) / (10000 + entity.TaxRate),
                UnitPriceCurrency = entity.Currency,
                LineTotalCents = (entity.Price * quantity * 10000) / (10000 + entity.TaxRate),
                LineTotalCurrency = entity.Currency
            };
        }

        public async Task<PricedCartItem> QuoteAsync(int quantity, CancellationToken cancellationToken)
        {
            var entity = await this.GetEntityAsync(cancellationToken);
            return new PricedCartItem
            {
                Listing = entity.ID,
                Option = entity.Option,
                Quantity = quantity,
                Was = entity.WasPrice,
                Now = entity.Price,
                TaxInfo = new Tax(entity.TaxRate, (TaxKind)entity.TaxKind),
                Currency = entity.Currency,
            };
        }
    }
}
