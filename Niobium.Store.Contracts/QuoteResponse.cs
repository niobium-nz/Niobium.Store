using System.Diagnostics.CodeAnalysis;
using Niobium.Finance;

namespace Niobium.Store
{
    public class QuoteResponse : QuoteRequest
    {
        [method: SetsRequiredMembers]
        public QuoteResponse(QuoteRequest request)
        {
            this.ID = request.ID;
            this.Cart = request.Cart;
            this.Captcha = String.Empty;
            this.Shipping = request.Shipping;
            this.ShippingCountry = request.ShippingCountry;
        }

        public Amount GrandTotal { get; set; }

        public Amount SubTotal { get; set; }

        public Amount ShippingCost { get; set; }

        public Amount Discount { get; set; }

        public TaxableAmount Tax { get; set; }
    }
}
