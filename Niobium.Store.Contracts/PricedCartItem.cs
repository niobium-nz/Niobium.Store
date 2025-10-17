using Niobium.Finance;

namespace Niobium.Store
{
    public class PricedCartItem : CartItem
    {
        public required long Was { get; set; }

        public required long Now { get; set; }

        public required long LineTotal { get; set; }

        public required long Discount { get; set; }

        public required Tax Tax { get; set; }

        public required Currency Currency { get; set; }

        public void Update()
        {
            LineTotal = Now * Quantity;
            Discount = (Was - Now) * Quantity;
            if (Discount < 0)
            {
                Discount = 0;
            }
        }
    }
}
