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

        public static string GetTenant(this Transaction transaction) => transaction == null || String.IsNullOrWhiteSpace(transaction.Tenant)
                ? throw new InvalidOperationException("Transaction does not contain tenant info.")
                : transaction.Tenant;
    }
}
