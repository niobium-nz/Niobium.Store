namespace Niobium.Store.Domains
{
    public class PromotionDomain(
        Lazy<IRepository<Promotion>> repository,
        IEnumerable<IDomainEventHandler<IDomain<Promotion>>> eventHandlers)
        : GenericDomain<Promotion>(repository, eventHandlers)
    {
        private const int PromotionalListing_BUY1GET1FREE = 1;
        private const int PromotionalListing_BUY2GET3FREE = 1;
        private const int PromotionalListing_BUY2GET3FREE_GIFT = 2;

        public Task ApplyAsync(QuoteResponse quote, CancellationToken cancellationToken = default)
        {
            if (this.RowKey == "BUY1GET1FREE")
            {
                var qualifiedItem = quote.Quote.FirstOrDefault(i => i.Listing == PromotionalListing_BUY1GET1FREE);
                if (qualifiedItem == null || qualifiedItem.Quantity <= 1)
                {
                    return Task.CompletedTask;
                }

                var wasQuantity = qualifiedItem.Quantity;
                var newQuantity = wasQuantity - 1;
                if (newQuantity <= 0)
                {
                    newQuantity = 1;
                }

                quote.Discount = (wasQuantity - newQuantity) * qualifiedItem.Now;
                quote.DiscountDescription = ["Buy 1 Get 1 Free"];
                quote.Quote.RemoveAll(i => i.Listing == PromotionalListing_BUY2GET3FREE_GIFT);
            }
            else if (this.RowKey == "BUY2GET3FREE")
            {
                var qualifiedItem = quote.Quote.FirstOrDefault(i => i.Listing == PromotionalListing_BUY2GET3FREE);
                if (qualifiedItem == null || qualifiedItem.Quantity <= 2)
                {
                    return Task.CompletedTask;
                }

                var wasQuantity = qualifiedItem.Quantity;
                var newQuantity = wasQuantity - 3;
                if (newQuantity < 2)
                {
                    newQuantity = 2;
                }

                quote.Discount = (wasQuantity - newQuantity) * qualifiedItem.Now;
                quote.DiscountDescription = ["Buy 2 Get 3 Free"];

                var giftItem = quote.Quote.FirstOrDefault(i => i.Listing == PromotionalListing_BUY2GET3FREE_GIFT);
                if (giftItem != null)
                {
                    giftItem.Quantity = 4;
                    quote.DiscountDescription.Add("Get 4 Hair Remover Free");
                }
            }
            else
            {
                quote.Quote.RemoveAll(i => i.Listing == PromotionalListing_BUY2GET3FREE_GIFT);
            }

            return Task.CompletedTask;
        }
    }
}
