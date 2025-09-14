namespace Niobium.Store
{
    public class Promotion : ITrackable
    {
        [EntityKey(EntityKeyKind.PartitionKey)]
        public required Guid Tenant { get; set; }

        [EntityKey(EntityKeyKind.RowKey)]
        public required string Code { get; set; }

        [EntityKey(EntityKeyKind.Timestamp)]
        public DateTimeOffset? Timestamp { get; set; }

        [EntityKey(EntityKeyKind.ETag)]
        public string? ETag { get; set; }

        public DateTimeOffset? Created { get; set; }
    }
}
