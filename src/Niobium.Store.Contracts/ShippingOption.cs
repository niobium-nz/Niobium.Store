using System.Diagnostics.CodeAnalysis;

namespace Niobium.Store
{
    [method: SetsRequiredMembers]
    public class ShippingOption()
    {
        [EntityKey(EntityKeyKind.PartitionKey)]
        public required string PartitionKey { get; set; }

        [EntityKey(EntityKeyKind.RowKey)]
        public required int ID { get; set; }

        [EntityKey(EntityKeyKind.Timestamp)]
        public DateTimeOffset? Timestamp { get; set; }

        [EntityKey(EntityKeyKind.ETag)]
        public string? ETag { get; set; }

        public required string Name { get; set; }

        public required long Price { get; set; }

        public required string Currency { get; set; }

        public required string Description { get; set; }

        public required string Countries { get; set; }

        public string[] GetCountries() => String.IsNullOrWhiteSpace(this.Countries)
                ? []
                : this.Countries.Split(GetCountriesSplitor(), StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        public static string BuildPartitionKey() => "dummy";

        public static string BuildRowKey(int id) => id.ToString();

        public static char GetCountriesSplitor() => ',';
    }
}
