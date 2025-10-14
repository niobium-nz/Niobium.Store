using System.ComponentModel.DataAnnotations;

namespace Niobium.Store
{
    public class QuoteRequest : IUserInput, IValidatableObject
    {
        [Required]
        public required Guid ID { get; set; }

        public Guid Tenant { get; set; }

        [Required]
        public required List<CartItem> Cart { get; set; } = [];

        [StringLength(20)]
        public string? Coupon { get; set; }

        [Required]
        [MaxLength(5000)]
        public required string Captcha { get; set; }

        [Required]
        [Range(1, 9999)]
        public int Shipping { get; set; }

        [StringLength(20)]
        public required string ShippingCountry { get; set; }

        public virtual IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (this.Tenant == Guid.Empty)
            {
                yield return new ValidationResult("Tenant is required.", [nameof(this.Tenant)]);
            }

            if (!Country.TryParse(this.ShippingCountry, out _))
            {
                yield return new ValidationResult($"Invalid country code: {this.ShippingCountry}", [nameof(this.ShippingCountry)]);
            }

            if (this.Cart.Count == 0)
            {
                yield return new ValidationResult($"No valid listings found from the order: {this.ID}", [nameof(this.Cart)]);
            }
        }

        public virtual void Sanitize()
        {
            this.Coupon = this.Coupon?.Trim().ToUpperInvariant();
            this.Captcha = this.Captcha.Trim();
            if (Country.TryParse(this.ShippingCountry, out var country))
            {
                this.ShippingCountry = country.Alpha2;
            }
        }
    }
}
