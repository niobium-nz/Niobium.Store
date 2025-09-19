using System.Diagnostics.CodeAnalysis;

namespace Niobium.Store
{
    [method: SetsRequiredMembers]
    public class Ownership() : ITrackable
    {
        [EntityKey(EntityKeyKind.PartitionKey)]
        public required string Email { get; set; }

        [EntityKey(EntityKeyKind.RowKey)]
        public required long Order { get; set; }

        [EntityKey(EntityKeyKind.Timestamp)]
        public DateTimeOffset? Timestamp { get; set; }

        [EntityKey(EntityKeyKind.ETag)]
        public string? ETag { get; set; }

        public DateTimeOffset? Created { get; set; }

        public required Guid Tenant { get; set; }

        public required Guid Customer { get; set; }

        public static string BuildPartitionKey(string email) => email.Trim().ToLowerInvariant();

        public static string BuildRowKey(long order) => order.ToString();
    }
}
