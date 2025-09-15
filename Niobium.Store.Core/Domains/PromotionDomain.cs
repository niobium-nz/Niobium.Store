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
                if (qualifiedItem == null)
                {
                    return Task.CompletedTask;
                }

                var newQuantity = qualifiedItem.Quantity - 1;
                if (newQuantity <= 0)
                {
                    newQuantity = 1;
                }

                qualifiedItem.Was = qualifiedItem.Now;
                qualifiedItem.Now = qualifiedItem.Unit * newQuantity;
                qualifiedItem.Discount = qualifiedItem.Was - qualifiedItem.Now;
                if (qualifiedItem.Discount < 0)
                {
                    qualifiedItem.Discount = 0;
                }
                qualifiedItem.DiscountDescription = $"Buy 1 Get 1 Free";
            }
            else if (this.RowKey == "BUY2GET3FREE")
            {
                var qualifiedItem = quote.Quote.FirstOrDefault(i => i.Listing == PromotionalListing_BUY2GET3FREE);
                if (qualifiedItem == null || qualifiedItem.Quantity <= 2)
                {
                    return Task.CompletedTask;
                }

                var newQuantity = qualifiedItem.Quantity - 3;
                if (newQuantity < 2)
                {
                    newQuantity = 2;
                }

                qualifiedItem.Was = qualifiedItem.Now;
                qualifiedItem.Now = qualifiedItem.Unit * newQuantity;
                qualifiedItem.Discount = qualifiedItem.Was - qualifiedItem.Now;
                if (qualifiedItem.Discount < 0)
                {
                    qualifiedItem.Discount = 0;
                }
                qualifiedItem.DiscountDescription = $"Buy 2 Get 3 Free";

                var giftItem = quote.Quote.FirstOrDefault(i => i.Listing == PromotionalListing_BUY2GET3FREE_GIFT);
                if (giftItem == null)
                {
                    return Task.CompletedTask;
                }

                var newGiftQuantity = giftItem.Quantity - 4;
                if (newGiftQuantity < 0)
                {
                    newGiftQuantity = 0;
                }

                giftItem.Was = giftItem.Now;
                giftItem.Now = giftItem.Unit * newGiftQuantity;
                giftItem.Discount = giftItem.Was - giftItem.Now;
                if (giftItem.Discount < 0)
                {
                    giftItem.Discount = 0;
                }
                giftItem.DiscountDescription = $"Get 4 Hair Remover Free";
            }

            return Task.CompletedTask;
        }
    }
}
