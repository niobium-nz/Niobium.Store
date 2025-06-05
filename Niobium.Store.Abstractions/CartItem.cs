using System.ComponentModel.DataAnnotations;

namespace Niobium.Store
{
    public class CartItem
    {
        [Range(1, 9999)]
        public int Listing { get; set; }

        [StringLength(50)]
        public string? Option { get; set; }

        [Range(1, 10)]
        public int Quantity { get; set; }
    }
}
