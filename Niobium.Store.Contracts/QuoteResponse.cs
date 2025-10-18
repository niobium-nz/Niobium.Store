using System.Diagnostics.CodeAnalysis;
using Niobium.Finance;

namespace Niobium.Store
{
    public class QuoteResponse : QuoteRequest
    {
        [method: SetsRequiredMembers]
        public QuoteResponse(QuoteRequest request, List<PricedCartItem> listingQuotes, TaxableAmount shippingQuote)
        {
            this.ID = request.ID;
            this.Cart = [];
            this.Captcha = String.Empty;
            this.Shipping = request.Shipping;
            this.ShippingCountry = request.ShippingCountry;
            this.Coupon = request.Coupon;

            this.Quote = listingQuotes;
            this.ShippingCost = shippingQuote.Amount;
            this.Discount = 0;

            var baseline = listingQuotes.First();
            this.Currency = baseline.Currency;
            this.TaxInfo = baseline.Tax;
        }

        public required List<PricedCartItem> Quote { get; set; }

        public long GrandTotal { get; set; }

        public long SubTotal { get; set; }

        public long ShippingCost { get; set; }

        public long Discount { get; set; }

        public Dictionary<int, string> DiscountDescription { get; set; } = [];

        public long Tax { get; set; }

        public Tax TaxInfo { get; set; }

        public Currency Currency { get; set; }

        public void Update()
        {
            this.SubTotal = this.Quote.Sum(x => x.LineTotal);
            var amountSubjectToTax = this.SubTotal + this.ShippingCost - this.Discount;
            this.Tax = amountSubjectToTax * this.TaxInfo.Rate / 10000;
            this.GrandTotal = amountSubjectToTax + this.Tax;
        }
    }
}
