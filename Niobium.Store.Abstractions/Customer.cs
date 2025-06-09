using Cod;

namespace Niobium.Store
{
    public class Customer : ITrackable
    {
        [EntityKey(EntityKeyKind.PartitionKey)]
        public required string Prefix { get; set; }

        [EntityKey(EntityKeyKind.RowKey)]
        public required Guid ID { get; set; }

        [EntityKey(EntityKeyKind.Timestamp)]
        public DateTimeOffset? Timestamp { get; set; }

        [EntityKey(EntityKeyKind.ETag)]
        public string? ETag { get; set; }

        public DateTimeOffset? Created { get; set; }

        public required string Currency { get; set; }

        public required string Culture { get; set; }

        public required string TimeZone { get; set; }

        public required string Consignee { get; set; }

        public required string Email { get; set; }

        public string? Phone { get; set; }

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

        public static string BuildPartitionKey(Guid id) => id.ToString()[..6];

        public static Guid ParseID(string partitionKey) => Guid.Parse(partitionKey);

        public static string BuildRowKey(Guid id) => id.ToString();
    }
}
