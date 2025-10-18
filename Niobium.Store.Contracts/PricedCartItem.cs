using Niobium.Finance;

namespace Niobium.Store
{
    public class PricedCartItem : CartItem
    {
        /// <summary>
        /// The original price per unit include tax in cents.
        /// </summary>
        public required long Was { get; set; }

        /// <summary>
        /// The current price per unit include tax in cents.
        /// </summary>
        public required long Now { get; set; }

        /// <summary>
        /// The tax information for this item.
        /// </summary>
        public required Tax TaxInfo { get; set; }

        /// <summary>
        /// The currency for this item.
        /// </summary>
        public required Currency Currency { get; set; }

        /// <summary>
        /// The tax amount per unit in cents.
        /// </summary>
        public long Tax => Now - Now * 10000 / (10000 + TaxInfo.Rate);

        /// <summary>
        /// The total amount based on <see cref="Now"/> include tax for the current line in cents.
        /// </summary>
        public long LineTotal => Now * Quantity;

        /// <summary>
        /// The total tax amount for the current line in cents.
        /// </summary>
        public long LineTax => LineTotal - LineTotal * 10000 / (10000 + TaxInfo.Rate);

        /// <summary>
        /// The total discount amount include tax for the current line in cents.
        /// </summary>
        public long Discount => (Was - Now) * Quantity;
    }
}
