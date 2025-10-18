using System.Diagnostics.CodeAnalysis;

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

        public long Settled { get; set; }

        public long Total { get; set; }

        public long Discount { get; set; }

        public long ShippingCost { get; set; }

        public long Tax { get; set; }

        public long TaxRate { get; set; }

        public int TaxKind { get; set; }

        public string? Coupon { get; set; }

        public string? Notes { get; set; }

        public required Guid Tenant { get; set; }

        public required string Cart { get; set; }

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

        public string? BillingSuburb { get; set; }

        public required string BillingCity { get; set; }

        public string? BillingState { get; set; }

        public required string BillingCountry { get; set; }

        public required string BillingPostcode { get; set; }

        public string? IP { get; set; }

        public string? Transactions { get; set; }

        public bool MarketingSubscription { get; set; }

        public string? Track { get; set; }

        public long GetID() => ParseID(this.Created);

        public string GetFullID() => BuildFullID(this.Customer, this.GetID());

        public CartItem[] GetCart() => !String.IsNullOrWhiteSpace(this.Cart)
                ? JsonMarshaller.Unmarshall<CartItem[]>(this.Cart) ?? []
                : [];

        public void SetCart(IEnumerable<CartItem> items) => this.Cart = JsonMarshaller.Marshall(items);

        public static string BuildFullID(Guid customer, long id) => BuildFullID(customer.ToString(), id.ToString());

        public static (Guid customer, long id) ParseFullID(string input)
        {
            var id = StorageKey.Parse(input);
            return (Guid.Parse(id.PartitionKey), Int64.Parse(id.RowKey));
        }

        public static string BuildFullID(string partitionKey, string rowKey)
            => new StorageKey(partitionKey, rowKey).ToString();

        public static long ParseID(DateTimeOffset created) => created.ToReverseUnixTimeMilliseconds();

        public static string BuildPartitionKey(Guid customer) => customer.ToString();

        public static string BuildRowKey(long id) => id.ToReverseUnixTimestamp();

        public static string BuildRowKey(DateTimeOffset time) => time.ToReverseUnixTimestamp();

        // Use order ID + 1 as the transaction ID for settlement as convention.
        public static long BuildTransactionID(long order) => order + 1;
    }
}
