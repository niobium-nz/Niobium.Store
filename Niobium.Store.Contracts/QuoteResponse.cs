using System.Diagnostics.CodeAnalysis;
using Niobium.Finance;

namespace Niobium.Store
{
    public class QuoteResponse : QuoteRequest
    {
        [method: SetsRequiredMembers]
        public QuoteResponse(QuoteRequest request, List<PricedCartItem> listingQuotes, ShippingQuote shippingQuote)
        {
            this.ID = request.ID;
            this.Cart = [];
            this.Captcha = String.Empty;
            this.Shipping = request.Shipping;
            this.ShippingCountry = request.ShippingCountry;
            this.Coupon = request.Coupon;

            this.Quote = listingQuotes;
            this.ShippingCost = shippingQuote.Cost.Amount;
            this.ShippingDescription = shippingQuote.Description;
            this.Discount = 0;

            var baseline = listingQuotes.First();
            this.Currency = baseline.Currency;
            this.TaxInfo = baseline.TaxInfo;
        }

        public required List<PricedCartItem> Quote { get; set; }

        public long ShippingCost { get; set; }

        public string ShippingDescription { get; set; }

        public long Discount { get; set; }

        public Dictionary<int, string> DiscountDescription { get; set; } = [];

        public Tax TaxInfo { get; set; }

        public Currency Currency { get; set; }

        public long Tax => Total - (long)Math.Round(((Total * 10000) / (10000m + TaxInfo.Rate)), 0, MidpointRounding.AwayFromZero);

        public long Subtotal => this.Quote.Sum(x => x.LineTotal);

        public long Total => this.Subtotal + this.ShippingCost - this.Discount;
    }
}
