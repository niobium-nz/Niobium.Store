using Microsoft.Extensions.Logging;
using Niobium.Store.Domains;

namespace Niobium.Store.Flows
{
    public class QuoteFlow(
        IDomainRepository<ShippingOptionDomain, ShippingOption> shippingRepo,
        IDomainRepository<ListingDomain, Listing> listingRepo,
        IDomainRepository<PromotionDomain, Promotion> promotionRepo,
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

            var listingQuotes = new List<PricedCartItem>();
            foreach (var item in request.Cart)
            {
                var listing = await listingRepo.GetAsync(
                    Listing.BuildPartitionKey(item.Listing),
                    Listing.BuildRowKey(item.Option),
                    cancellationToken: cancellationToken);
                var listingQuote = await listing.QuoteAsync(item.Quantity, cancellationToken);
                listingQuotes.Add(listingQuote);
            }

            var listingCurrency = listingQuotes.First().Currency;
            if (shippingQuote.Amount.Currency != listingCurrency)
            {
                var error = $"Shipping {request.Shipping} and cart are not consistent on currency: {shippingQuote.Amount.Currency} vs {listingCurrency}";
                logger.LogError(error);
                throw new ApplicationException(InternalError.BadRequest, error) { Reference = error };
            }

            var listingTax = listingQuotes.First().Tax;
            if (shippingQuote.Tax != listingTax)
            {
                var error = $"Shipping {request.Shipping} and cart are not consistent on tax: {shippingQuote.Tax} vs {listingTax}";
                logger.LogError(error);
                throw new ApplicationException(InternalError.BadRequest, error) { Reference = error };
            }

            var result = new QuoteResponse(request, listingQuotes, shippingQuote);
            if (!String.IsNullOrWhiteSpace(request.Coupon))
            {
                var promotion = new Promotion { Tenant = request.Tenant, Code = request.Coupon.Trim() };
                var promoDomain = await promotionRepo.GetAsync(promotion, cancellationToken: cancellationToken);
                await promoDomain.ApplyAsync(result, cancellationToken);
            }

            result.Update();
            return result;
        }
    }
}
