using System.Security.Cryptography.Xml;
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
                UnitPriceCents = entity.Price,
                UnitPriceCurrency = entity.Currency,
                LineTotalCents = entity.Price * quantity,
                LineTotalCurrency = entity.Currency
            };
        }

        public async Task<PricedCartItem> QuoteAsync(int quantity, CancellationToken cancellationToken)
        {
            var entity = await this.GetEntityAsync(cancellationToken);
            var result = new PricedCartItem
            {
                Listing = entity.ID,
                Option = entity.Option,
                Quantity = quantity,
                Was = entity.WasPrice,
                Now = entity.Price,
                Tax = new Tax(entity.TaxRate, (TaxKind)entity.TaxKind),
                Currency = entity.Currency,
                LineTotal = 0,
                Discount = 0,
            };
            result.Update();
            return result;
        }
    }
}
