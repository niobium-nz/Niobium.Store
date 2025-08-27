using System.ComponentModel.DataAnnotations;
using Niobium;

namespace Niobium.Store
{
    public class QuoteRequest : IUserInput, IValidatableObject
    {
        [Required]
        public required Guid ID { get; set; }

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
            if (!Country.TryParse(ShippingCountry, out _))
            {
                yield return new ValidationResult($"Invalid country code: {ShippingCountry}", [nameof(ShippingCountry)]);
            }

            if (Cart.Count == 0)
            {
                yield return new ValidationResult($"No valid listings found from the order: {ID}", [nameof(Cart)]);
            }
        }

        public virtual void Sanitize()
        {
            Coupon = Coupon?.Trim().ToUpperInvariant();
            Captcha = Captcha.Trim();
            if (Country.TryParse(ShippingCountry, out var country))
            {
                ShippingCountry = country.Alpha2;
            }
        }
    }
}
