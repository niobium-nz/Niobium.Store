using System.ComponentModel.DataAnnotations;

namespace Niobium.Store
{
    public class OrderRequest : Order
    {
        [Required]
        public required Guid ID { get; set; }

        [Required]
        public required List<CartItem> Cart { get; set; } = [];

        [Required]
        [MaxLength(5000)]
        public required string Captcha { get; set; }

        [Required]
        [Range(1, 9999)]
        public int Shipping { get; set; }
    }
}
