using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Niobium.Finance;

namespace Niobium.Store.Domains
{
    public class PromotionDomain(
        Lazy<IRepository<Promotion>> repository,
        IEnumerable<IDomainEventHandler<IDomain<Promotion>>> eventHandlers)
        : GenericDomain<Promotion>(repository, eventHandlers)
    {
        private const int PromotionalListing = 1;

        public Task ApplyAsync(QuoteResponse quote, CancellationToken cancellationToken = default)
        {
            if (this.RowKey == "BUY1GET1FREE")
            {
                var qualifiedItems = quote.Quote.Where(i => i.Listing == PromotionalListing).ToList();
                foreach (var item in qualifiedItems)
                {
                    var discountQty = item.Quantity / 2;
                    if (discountQty < 0)
                    {
                        discountQty = 0;
                    }
                    if (discountQty > item.Quantity)
                    {
                        item.Quantity = item.Quantity;
                    }

                    item.Was = item.Now;
                    item.Now = new Amount { Cents = item.Unit.Cents / (item.Quantity - discountQty), Currency = item.Unit.Currency };
                    item.Discount = new Amount { Cents = item.Was.Cents - item.Now.Cents, Currency = item.Unit.Currency };
                    item.DiscountDescription = $"Buy 1 Get 1 Free";
                }
            }
            else if (this.RowKey == "BUY2GET3FREE")
            {
                var qualifiedItems = quote.Quote.Where(i => i.Listing == PromotionalListing).ToList();
                foreach (var item in qualifiedItems)
                {
                    var discountQty = (item.Quantity / 5) * 3;
                    if (discountQty < 0)
                    {
                        discountQty = 0;
                    }
                    if (discountQty > item.Quantity)
                    {
                        item.Quantity = item.Quantity;
                    }

                    item.Was = item.Now;
                    item.Now = new Amount { Cents = item.Unit.Cents / (item.Quantity - discountQty), Currency = item.Unit.Currency };
                    item.Discount = new Amount { Cents = item.Was.Cents - item.Now.Cents, Currency = item.Unit.Currency };
                    item.DiscountDescription = $"Buy 2 Get 3 Free";
                }
            }

            return Task.CompletedTask;
        }
    }
}
