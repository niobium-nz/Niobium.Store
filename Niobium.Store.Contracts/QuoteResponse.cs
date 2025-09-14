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
            this.Cart = request.Cart;
            this.Captcha = String.Empty;
            this.Shipping = request.Shipping;
            this.ShippingCountry = request.ShippingCountry;

            this.Quote = listingQuotes;
            this.ShippingCost = shippingQuote.Amount;
            this.Discount = new Amount(0, listingQuotes.First().Currency);
        }

        public required List<PricedCartItem> Quote { get; set; }

        public Amount GrandTotal { get; set; }

        public Amount SubTotal { get; set; }

        public Amount ShippingCost { get; set; }

        public Amount Discount { get; set; }

        public TaxableAmount Tax { get; set; }

        public void Update()
        {
            var baseline = Quote.First();
            var currency = baseline.Unit.Currency;
            var tax = baseline.Tax;

            this.Discount = new Amount(Math.Max(0, Quote.Sum(x => x.Discount.Cents)), currency);
            this.SubTotal = new Amount { Cents = Quote.Sum(x => x.Was.Cents), Currency = currency };
            var amountSubjectToTax = this.SubTotal.Cents + this.ShippingCost.Cents - this.Discount.Cents;
            this.Tax = new TaxableAmount
            {
                Amount = new Amount { Cents = amountSubjectToTax * tax.Rate / 10000, Currency = currency },
                Tax = tax,
            };
            this.GrandTotal = new Amount { Cents = amountSubjectToTax + this.Tax.Amount.Cents, Currency = currency };
        }
    }
}
