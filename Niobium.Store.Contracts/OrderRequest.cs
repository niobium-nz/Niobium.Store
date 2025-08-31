using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace Niobium.Store
{
    public class OrderRequest : QuoteRequest, IUserInput, IValidatableObject
    {
        [Required]
        public required long Timestamp { get; set; }

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

        [StringLength(20)]
        public required string Tenant { get; set; }

        public bool MarketingSubscription { get; set; }

        [StringLength(10)]
        public string? Track { get; set; }

        public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var baseResults = base.Validate(validationContext);
            foreach (var baseResult in baseResults)
            {
                yield return baseResult;
            }

            if (this.Timestamp <= 0 || DateTimeOffset.UtcNow - DateTimeOffsetExtensions.FromReverseUnixTimeMilliseconds(this.Timestamp) > TimeSpan.FromMinutes(20))
            {
                yield return new ValidationResult($"Invalid order time on the order: {this.ID}", [nameof(this.Cart)]);
            }
        }

        public override void Sanitize()
        {
            base.Sanitize();

            if (!CultureInfoExtensions.TryParseCultureInfo(this.Culture, out var culture))
            {
                culture = new CultureInfo("en-US");
            }

            this.Culture = culture.Name;
            this.Notes = this.Notes?.Trim();
            this.Consignee = culture.ToTitleCase(this.Consignee.Trim());

            //TODO (whan) Phone number -> E.164 format
            //TODO (whan) Postcode -> validate according to specific country

            this.ShippingAddressLine1 = culture.ToTitleCase(this.ShippingAddressLine1.Trim());
            if (this.ShippingAddressLine2 != null)
            {
                this.ShippingAddressLine2 = culture.ToTitleCase(this.ShippingAddressLine2.Trim());
            }

            if (this.ShippingSuburb != null)
            {
                this.ShippingSuburb = culture.ToTitleCase(this.ShippingSuburb.Trim());
            }

            this.ShippingCity = culture.ToTitleCase(this.ShippingCity.Trim());

            if (this.ShippingState != null)
            {
                this.ShippingState = culture.ToTitleCase(this.ShippingState.Trim());
            }

            this.ShippingPostcode = this.ShippingPostcode.Trim();
            this.BillingName = culture.ToTitleCase(this.BillingName.Trim());

            if (this.BillingBusiness != null)
            {
                this.BillingBusiness = culture.ToTitleCase(this.BillingBusiness.Trim());
            }

            this.BillingAddressLine1 = culture.ToTitleCase(this.BillingAddressLine1.Trim());
            if (this.BillingAddressLine2 != null)
            {
                this.BillingAddressLine2 = culture.ToTitleCase(this.BillingAddressLine2.Trim());
            }
            if (this.BillingSuburb != null)
            {
                this.BillingSuburb = culture.ToTitleCase(this.BillingSuburb.Trim());
            }
            this.BillingCity = culture.ToTitleCase(this.BillingCity.Trim());

            if (this.BillingState != null)
            {
                this.BillingState = culture.ToTitleCase(this.BillingState.Trim());
            }

            this.BillingPostcode = this.BillingPostcode.Trim();
            this.Email = this.Email.Trim().ToLowerInvariant();

            if (Country.TryParse(this.BillingCountry, out var country))
            {
                this.BillingCountry = country.Alpha2;
            }

            if (!string.IsNullOrWhiteSpace(Track))
            {
                Track = Track.Trim();
            }
        }
    }
}
