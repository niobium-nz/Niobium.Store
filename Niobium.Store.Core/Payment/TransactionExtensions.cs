using Niobium.Finance;

namespace Niobium.Store.Payment
{
    internal static class TransactionExtensions
    {
        public static long GetOrder(this Transaction transaction) => transaction == null || !Int64.TryParse(transaction.Reference, out var order)
                ? throw new InvalidOperationException("Transaction does not contain an order ID.")
                : order;

        public static Guid GetCustomer(this Transaction transaction) => transaction == null || !Guid.TryParse(transaction.PartitionKey, out var user)
                ? throw new InvalidOperationException("Transaction does not contain an user ID.")
                : user;

        public static Guid GetTenant(this Transaction transaction) => transaction == null || !Guid.TryParse(transaction.Tenant, out var tenant)
                ? throw new InvalidOperationException("Transaction does not contain tenant ID.")
                : tenant;
    }
}
