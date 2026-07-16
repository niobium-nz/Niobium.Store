using System.Diagnostics.CodeAnalysis;

namespace Niobium.Store
{
    [method: SetsRequiredMembers]
    public class Listing() : ITrackable
    {
        [EntityKey(EntityKeyKind.PartitionKey)]
        public required int ID { get; set; }

        [EntityKey(EntityKeyKind.RowKey)]
        public required string Option { get; set; }

        [EntityKey(EntityKeyKind.Timestamp)]
        public DateTimeOffset? Timestamp { get; set; }

        [EntityKey(EntityKeyKind.ETag)]
        public string? ETag { get; set; }

        public DateTimeOffset? Created { get; set; }

        public required string Name { get; set; }

        public required long Price { get; set; }

        public required long WasPrice { get; set; }

        public required string Currency { get; set; }

        public required string SKU { get; set; }

        public long TaxRate { get; set; }

        public int TaxKind { get; set; }

        public required string ShippingOptions { get; set; }

        public required string Culture { get; set; }

        public string? Note { get; set; }

        public string[] GetShippingOptions() => !String.IsNullOrWhiteSpace(this.ShippingOptions)
                ? this.ShippingOptions.Split(GetShippingOptionsSplitor(), StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : [];

        public string GetFullID() => BuildFullID(this.ID, this.Option);

        public static char GetShippingOptionsSplitor() => ',';

        public static string GetDefaultOption() => "Default";

        public static string BuildPartitionKey(int id) => id.ToString();

        public static string BuildRowKey(string? option) => option ?? GetDefaultOption();

        public static string BuildFullID(int listingID, string option) => BuildFullID(listingID.ToString(), option);

        public static (int listingID, string option) ParseFullID(string input)
        {
            var id = StorageKey.Parse(input);
            return (Int32.Parse(id.PartitionKey), id.RowKey);
        }

        public static string BuildFullID(string partitionKey, string rowKey)
            => new StorageKey(partitionKey, rowKey).ToString();
    }
}
