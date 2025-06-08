using System.ComponentModel.DataAnnotations;

namespace Niobium.Store
{
    public class OrderRequest
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

        [StringLength(20)]
        public string? Coupon { get; set; }

        [StringLength(100)]
        public string? Notes { get; set; }

        [StringLength(10)]
        public required string Culture { get; set; }

        [StringLength(20)]
        public required string TimeZone { get; set; }

        [StringLength(50)]
        public required string Consignee { get; set; }

        [StringLength(50)]
        [EmailAddress]
        public required string Email { get; set; }

        [StringLength(20)]
        [Phone]
        public string? Phone { get; set; }

        [StringLength(50)]
        public required string ShippingAddressLine1 { get; set; }

        [StringLength(50)]
        public string? ShippingAddressLine2 { get; set; }

        [StringLength(20)]
        public string? ShippingSuburb { get; set; }

        [StringLength(20)]
        public required string ShippingCity { get; set; }

        [StringLength(20)]
        public string? ShippingState { get; set; }

        [StringLength(20)]
        public required string ShippingCountry { get; set; }

        [StringLength(10)]
        public required string ShippingPostcode { get; set; }

        [StringLength(50)]
        public required string BillingName { get; set; }

        [StringLength(50)]
        public string? BillingBusiness { get; set; }

        [StringLength(50)]
        public required string BillingAddressLine1 { get; set; }

        [StringLength(50)]
        public string? BillingAddressLine2 { get; set; }

        [StringLength(20)]
        public string? BillingSuburb { get; set; }

        [StringLength(20)]
        public required string BillingCity { get; set; }

        [StringLength(20)]
        public string? BillingState { get; set; }

        [StringLength(20)]
        public required string BillingCountry { get; set; }

        [StringLength(10)]
        public required string BillingPostcode { get; set; }
    }
}
