using Microsoft.Extensions.Logging;
using Niobium.Finance;
using Niobium.Store.Domains;

namespace Niobium.Store.Flows
{
    public class QuoteFlow(
        IDomainRepository<ShippingOptionDomain, ShippingOption> shippingRepo,
        IDomainRepository<ListingDomain, Listing> listingRepo,
        ILogger<QuoteFlow> logger)
        : IFlow
    {
        public async Task<QuoteResponse> RunAsync(QuoteRequest request, CancellationToken cancellationToken)
        {
            var shipping = await shippingRepo.GetAsync(
                ShippingOption.BuildPartitionKey(),
                ShippingOption.BuildRowKey(request.Shipping),
                cancellationToken: cancellationToken);

            var shippingQuote = await shipping.QuoteAsync(request, cancellationToken);

            var listingQuotes = new List<TaxableAmount>();
            foreach (var item in request.Cart)
            {
                var listing = await listingRepo.GetAsync(
                    Listing.BuildPartitionKey(item.Listing),
                    Listing.BuildRowKey(item.Option),
                    cancellationToken: cancellationToken);
                var listingQuote = await listing.QuoteAsync(item.Quantity, cancellationToken);
                listingQuotes.Add(listingQuote);
            }

            var valid = listingQuotes.ValidateConsistency();
            if (!valid)
            {
                var error = $"Listings are not consistent on either currency or tax: {string.Join(',', request.Cart.Select(i => i.Listing))}";
                logger.LogError(error);
                throw new ApplicationException(InternalError.BadRequest, error) { Reference = error };
            }

            var baseline = listingQuotes.First();
            var currency = baseline.Amount.Currency;
            var tax = baseline.Tax;

            if (shippingQuote.Amount.Currency != currency)
            {
                var error = $"Shipping {request.Shipping} and cart are not consistent on currency: {shippingQuote.Amount.Currency} vs {currency}";
                logger.LogError(error);
                throw new ApplicationException(InternalError.BadRequest, error) { Reference = error };
            }

            var result = new QuoteResponse(request)
            {
                Discount = new Amount { Cents = 0, Currency = currency }, // Assuming no discount for simplicity, can be modified to apply discounts if needed.
                SubTotal = new Amount { Cents = listingQuotes.Sum(x => x.Amount.Cents), Currency = currency },
                ShippingCost = shippingQuote.Amount,
            };
            var amountSubjectToTax = result.SubTotal.Cents + result.ShippingCost.Cents - result.Discount.Cents;
            result.Tax = new TaxableAmount
            { 
                Amount = new Amount { Cents = amountSubjectToTax * tax.Rate / 10000, Currency = currency },
                Tax = tax,
            };
            result.GrandTotal = new Amount { Cents = amountSubjectToTax + result.Tax.Amount.Cents, Currency = currency };
            return result;
        }
    }
}
