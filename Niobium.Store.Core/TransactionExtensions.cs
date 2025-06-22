using Cod.Finance;

namespace Niobium.Store
{
    internal static class TransactionExtensions
    {
        public static long GetOrder(this Transaction transaction)
        {
            if (transaction == null || !long.TryParse(transaction.Reference, out var order))
            {
                throw new InvalidOperationException("Transaction does not contain an order ID.");
            }

            return order;
        }

        public static Guid GetCustomer(this Transaction transaction)
        {
            if (transaction == null || !Guid.TryParse(transaction.PartitionKey, out var user))
            {
                throw new InvalidOperationException("Transaction does not contain an user ID.");
            }

            return user;
        }

        public static string GetTenant(this Transaction transaction)
        {
            if (transaction == null || string.IsNullOrWhiteSpace(transaction.Tenant))
            {
                throw new InvalidOperationException("Transaction does not contain tenant info.");
            }

            return transaction.Tenant;
        }
    }
}
