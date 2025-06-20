using System.Diagnostics.CodeAnalysis;
using Cod.Finance;

namespace Niobium.Store
{
    public class QuoteResponse : QuoteRequest
    {
        [method: SetsRequiredMembers]
        public QuoteResponse(QuoteRequest request)
        {
            ID = request.ID;
            Cart = request.Cart;
            Captcha = string.Empty;
            Shipping = request.Shipping;
            ShippingCountry = request.ShippingCountry;
        }

        public Amount GrandTotal { get; set; }

        public Amount SubTotal { get; set; }

        public Amount ShippingCost { get; set; }

        public Amount Tax { get; set; }

        public Amount Discount { get; set; }

        public long TaxRate { get; set; }

        public string? TaxKind { get; set; }
    }
}
