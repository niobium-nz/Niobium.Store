using Niobium.Finance;

namespace Niobium.Store
{
    public class PricedCartItem : CartItem
    {
        public required Amount Unit { get; set; }

        public required Amount Was { get; set; }

        public required Amount Now { get; set; }

        public required Amount Discount { get; set; }

        public required Tax Tax { get; set; }

        public required Currency Currency { get; set; }

        public string? DiscountDescription { get; set; }
    }
}
