using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Cod;

namespace Niobium.Store
{
    public class OrderRequest : IUserInput, IValidatableObject
    {
        [Required]
        public required Guid ID { get; set; }

        [Required]
        public required long Timestamp { get; set; }

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

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!Country.TryParse(ShippingCountry, out _))
            {
                yield return new ValidationResult($"Invalid country code: {ShippingCountry}", [nameof(ShippingCountry)]);
            }

            if (Cart.Count == 0)
            {
                yield return new ValidationResult($"No valid listings found from the order: {ID}", [nameof(Cart)]);
            }

            if (Timestamp <= 0 || DateTimeOffset.UtcNow - DateTimeOffsetExtensions.FromReverseUnixTimeMilliseconds(Timestamp) > TimeSpan.FromMinutes(20))
            {
                yield return new ValidationResult($"Invalid order time on the order: {ID}", [nameof(Cart)]);
            }
        }

        public void Sanitize()
        {
            if (!CultureInfoExtensions.TryParseCultureInfo(Culture, out var culture))
            {
                culture = new CultureInfo("en-US");
            }

            Culture = culture.Name;
            Captcha = Captcha.Trim();
            Coupon = Coupon?.Trim().ToUpperInvariant();
            Notes = Notes?.Trim();
            Consignee = culture.ToTitleCase(Consignee.Trim());

            //TODO (whan) Phone number -> E.164 format
            //TODO (whan) Postcode -> validate according to specific country

            ShippingAddressLine1 = culture.ToTitleCase(ShippingAddressLine1.Trim());
            if (ShippingAddressLine2 != null)
            {
                ShippingAddressLine2 = culture.ToTitleCase(ShippingAddressLine2.Trim());
            }

            if (ShippingSuburb != null)
            {
                ShippingSuburb = culture.ToTitleCase(ShippingSuburb.Trim());
            }

            ShippingCity = culture.ToTitleCase(ShippingCity.Trim());

            if (ShippingState != null)
            {
                ShippingState = culture.ToTitleCase(ShippingState.Trim());
            }

            ShippingPostcode = ShippingPostcode.Trim();
            BillingName = culture.ToTitleCase(BillingName.Trim());

            if (BillingBusiness != null)
            {
                BillingBusiness = culture.ToTitleCase(BillingBusiness.Trim());
            }

            BillingAddressLine1 = culture.ToTitleCase(BillingAddressLine1.Trim());
            if (BillingAddressLine2 != null)
            {
                BillingAddressLine2 = culture.ToTitleCase(BillingAddressLine2.Trim());
            }
            if (BillingSuburb != null)
            {
                BillingSuburb = culture.ToTitleCase(BillingSuburb.Trim());
            }
            BillingCity = culture.ToTitleCase(BillingCity.Trim());

            if (BillingState != null)
            {
                BillingState = culture.ToTitleCase(BillingState.Trim());
            }

            BillingPostcode = BillingPostcode.Trim();
            Email = Email.Trim().ToLowerInvariant();

            if (Country.TryParse(ShippingCountry, out var country))
            {
                ShippingCountry = country.Alpha2;
            }

            if (Country.TryParse(BillingCountry, out country))
            {
                BillingCountry = country.Alpha2;
            }
        }
    }
}
