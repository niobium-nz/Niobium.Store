using Cod;
using System.ComponentModel.DataAnnotations;

namespace Niobium.Store
{
    public class Order
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

        [StringLength(5)]
        public string? TaxKind { get; set; }

        [StringLength(20)]
        public string? Coupon { get; set; }

        [StringLength(100)]
        public string? Notes { get; set; }

        [Required]
        [StringLength(100)]
        public required string Items { get; set; }

        [Required]
        [StringLength(5)]
        public required string Currency { get; set; }

        [Required]
        [StringLength(10)]
        public required string Culture { get; set; }

        [Required]
        [StringLength(20)]
        public required string TimeZone { get; set; }

        [Required]
        [StringLength(50)]
        public required string Consignee { get; set; }

        [Required]
        [StringLength(50)]
        [EmailAddress]
        public required string Email { get; set; }

        [StringLength(20)]
        [Phone]
        public string? Phone { get; set; }

        public int ShippingStatus { get; set; }

        [Required]
        [StringLength(50)]
        public required string ShippingAddressLine1 { get; set; }

        [StringLength(50)]
        public string? ShippingAddressLine2 { get; set; }

        [StringLength(20)]
        public string? ShippingSuburb { get; set; }

        [Required]
        [StringLength(20)]
        public required string ShippingCity { get; set; }

        [StringLength(20)]
        public string? ShippingState { get; set; }

        [Required]
        [StringLength(20)]
        public required string ShippingCountry { get; set; }

        [Required]
        [StringLength(10)]
        public required string ShippingPostcode { get; set; }

        [Required]
        [StringLength(50)]
        public required string BillingName { get; set; }

        [StringLength(50)]
        public string? BillingBusiness { get; set; }

        [Required]
        [StringLength(50)]
        public required string BillingAddressLine1 { get; set; }

        [StringLength(50)]
        public string? BillingAddressLine2 { get; set; }

        [Required]
        [StringLength(20)]
        public required string BillingCity { get; set; }

        [StringLength(20)]
        public string? BillingState { get; set; }

        [Required]
        [StringLength(20)]
        public required string BillingCountry { get; set; }

        [Required]
        [StringLength(10)]
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
