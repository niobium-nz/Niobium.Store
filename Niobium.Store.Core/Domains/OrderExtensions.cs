using System.Globalization;
using System.Text;
using Niobium.Finance;

namespace Niobium.Store.Domains
{
    internal static class OrderExtensions
    {
        public static IReadOnlyDictionary<string, string> BuildNotificationParameters(this Listing listing, int quantity)
        {
            return new Dictionary<string, string>
            {
                { ToSnakeCaseUpper(nameof(listing.Name)), listing.Name },
                { ToSnakeCaseUpper(nameof(listing.Option)), listing.Option },
                { ToSnakeCaseUpper(nameof(listing.Price)), new Amount(listing.Price, listing.Currency).ToString() },
                { ToSnakeCaseUpper(nameof(listing.SKU)), listing.SKU },
                { "QUANTITY", quantity.ToString() },
            };
        }

        public static IReadOnlyDictionary<string, object> BuildNotificationParameters(this Order order)
        {
            var timezone = TimeZoneInfoHelper.ParseTimeZoneFromIANA(order.TimeZone);
            var culture = CultureInfo.GetCultureInfo(order.Culture, true);
            return new Dictionary<string, object>
            {
                { "ORDER_ID", order.GetID().ToString() },
                { "ORDER_DATE", order.Created.ToLocal(timezone).ToYearMonthDayInNames(culture) },
                { ToSnakeCaseUpper(nameof(order.Coupon)), order.Coupon ?? String.Empty },
                { ToSnakeCaseUpper(nameof(order.BillingAddressLine1)), order.BillingAddressLine1 },
                { ToSnakeCaseUpper(nameof(order.BillingAddressLine2)), order.BillingAddressLine2 ?? String.Empty },
                { ToSnakeCaseUpper(nameof(order.BillingBusiness)), order.BillingBusiness ?? String.Empty },
                { ToSnakeCaseUpper(nameof(order.BillingCity)), order.BillingCity },
                { ToSnakeCaseUpper(nameof(order.BillingCountry)), Country.Parse(order.BillingCountry).Fullname },
                { ToSnakeCaseUpper(nameof(order.BillingName)), order.BillingName },
                { ToSnakeCaseUpper(nameof(order.BillingPostcode)), order.BillingPostcode },
                { ToSnakeCaseUpper(nameof(order.BillingState)), order.BillingState ?? String.Empty },
                { ToSnakeCaseUpper(nameof(order.BillingSuburb)), order.BillingSuburb ?? String.Empty },
                { ToSnakeCaseUpper(nameof(order.Consignee)), order.Consignee },
                { ToSnakeCaseUpper(nameof(order.Discount)), new Amount(order.Discount, order.Currency).ToString() },
                { ToSnakeCaseUpper(nameof(order.Email)), order.Email },
                { ToSnakeCaseUpper(nameof(order.GrandTotal)), new Amount(order.GrandTotal, order.Currency).ToString() },
                { ToSnakeCaseUpper(nameof(order.Notes)), order.Notes ?? String.Empty },
                { ToSnakeCaseUpper(nameof(order.Phone)), order.Phone ?? String.Empty },
                { ToSnakeCaseUpper(nameof(order.ShippingAddressLine1)), order.ShippingAddressLine1 },
                { ToSnakeCaseUpper(nameof(order.ShippingAddressLine2)), order.ShippingAddressLine2 ?? String.Empty },
                { ToSnakeCaseUpper(nameof(order.ShippingCity)), order.ShippingCity },
                { ToSnakeCaseUpper(nameof(order.ShippingCost)), order.ShippingCost == 0 ? "FREE" : new Amount(order.ShippingCost, order.Currency).ToString() },
                { ToSnakeCaseUpper(nameof(order.ShippingCountry)), Country.Parse(order.ShippingCountry).Fullname },
                { ToSnakeCaseUpper(nameof(order.ShippingPostcode)), order.ShippingPostcode },
                { ToSnakeCaseUpper(nameof(order.ShippingState)), order.ShippingState ?? String.Empty },
                { ToSnakeCaseUpper(nameof(order.ShippingStatus)), ((ShippingStatus)order.ShippingStatus).ToString() },
                { ToSnakeCaseUpper(nameof(order.ShippingSuburb)), order.ShippingSuburb ?? String.Empty },
                { ToSnakeCaseUpper(nameof(order.Status)), ((OrderStatus)order.Status).ToString() },
                { ToSnakeCaseUpper(nameof(order.SubTotal)), new Amount(order.SubTotal, order.Currency).ToString() },
                { ToSnakeCaseUpper(nameof(order.Tax)), new Amount(order.Tax, order.Currency).ToString() },
                { ToSnakeCaseUpper(nameof(order.TaxKind)), ((TaxKind)order.TaxKind).ToString() },
                { ToSnakeCaseUpper(nameof(order.TaxRate)), $"{order.TaxRate/100}%" },
            };
        }

        private static string ToSnakeCaseUpper(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(text.Length * 2);
            sb.Append(char.ToUpperInvariant(text[0]));

            for (int i = 1; i < text.Length; i++)
            {
                char current = text[i];
                char previous = text[i - 1];
                char? next = i + 1 < text.Length ? text[i + 1] : (char?)null;

                bool isUpper = char.IsUpper(current);
                bool prevIsUpper = char.IsUpper(previous);
                bool prevIsLower = char.IsLower(previous);
                bool nextIsLower = next.HasValue && char.IsLower(next.Value);

                if (isUpper && (prevIsLower || (prevIsUpper && nextIsLower)))
                {
                    sb.Append('_');
                }

                sb.Append(char.ToUpperInvariant(current));
            }

            return sb.ToString();
        }
    }
}
