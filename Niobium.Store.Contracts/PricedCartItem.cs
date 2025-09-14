using Niobium.Finance;

namespace Niobium.Store
{
    public class PricedCartItem : CartItem
    {
        public required long Unit { get; set; }

        public required long Was { get; set; }

        public required long Now { get; set; }

        public required long Discount { get; set; }

        public required Tax Tax { get; set; }

        public required Currency Currency { get; set; }

        public string? DiscountDescription { get; set; }
    }
}
