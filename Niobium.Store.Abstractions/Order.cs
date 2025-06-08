using System.Diagnostics.CodeAnalysis;
using Cod;

namespace Niobium.Store
{
    [method: SetsRequiredMembers]
    public class Order()
    {
        [EntityKey(EntityKeyKind.PartitionKey)]
        public required Guid Customer { get; set; }

        [EntityKey(EntityKeyKind.RowKey)]
        public required DateTimeOffset Created { get; set; }

        [EntityKey(EntityKeyKind.Timestamp)]
        public DateTimeOffset? Timestamp { get; set; }

        [EntityKey(EntityKeyKind.ETag)]
        public string? ETag { get; set; }

        public int Status { get; set; }

        public long Paid { get; set; }

        public long GrandTotal { get; set; }

        public long SubTotal { get; set; }

        public long Discount { get; set; }

        public long ShippingCost { get; set; }

        public long Tax { get; set; }

        public long TaxRate { get; set; }

        public string? TaxKind { get; set; }

        public string? Coupon { get; set; }

        public string? Notes { get; set; }

        public required string Items { get; set; }

        public required string Currency { get; set; }

        public required string Culture { get; set; }

        public required string TimeZone { get; set; }

        public required string Consignee { get; set; }

        public required string Email { get; set; }

        public string? Phone { get; set; }

        public int ShippingStatus { get; set; }

        public required string ShippingAddressLine1 { get; set; }

        public string? ShippingAddressLine2 { get; set; }

        public string? ShippingSuburb { get; set; }

        public required string ShippingCity { get; set; }

        public string? ShippingState { get; set; }

        public required string ShippingCountry { get; set; }

        public required string ShippingPostcode { get; set; }

        public required string BillingName { get; set; }

        public string? BillingBusiness { get; set; }

        public required string BillingAddressLine1 { get; set; }

        public string? BillingAddressLine2 { get; set; }

        public required string BillingCity { get; set; }

        public string? BillingState { get; set; }

        public required string BillingCountry { get; set; }

        public required string BillingPostcode { get; set; }

        public string? IP { get; set; }

        public long GetID() => ParseID(Created);

        public string GetFullID() => BuildFullID(Customer, GetID());

        public string[] GetItems()
        {
            return string.IsNullOrWhiteSpace(Items)
                ? []
                : Items.Split(GetItemsSplitor(), StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        public static char GetItemsSplitor() => ',';

        public static string BuildFullID(Guid customer, long id) => BuildFullID(customer.ToString(), id.ToString());

        public static (Guid customer, long id) ParseFullID(string input)
        {
            var id = StorageKeyExtensions.ParseFullID(input);
            return (Guid.Parse(id.PartitionKey), long.Parse(id.RowKey));
        }

        public static string BuildFullID(string partitionKey, string rowKey)
            => new StorageKey { PartitionKey = partitionKey, RowKey = rowKey }.BuildFullID();

        public static long ParseID(DateTimeOffset created) => created.ToReverseUnixTimeMilliseconds();

        public static string BuildPartitionKey(Guid customer) => customer.ToString();

        public static string BuildRowKey(long id) => id.ToReverseUnixTimestamp();
    }
}
