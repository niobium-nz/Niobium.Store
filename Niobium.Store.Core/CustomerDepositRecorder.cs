using Cod;
using Cod.Finance;
using Cod.Platform.Finance;

namespace Niobium.Store
{
    internal class CustomerDepositRecorder(IDomainRepository<CustomerDomain, Customer> repo)
        : AccountDepositRecorder<CustomerDomain, Customer>(repo)
    {
        protected override string BuildPartitionKey(Transaction transaction)
        {
            if (transaction.Tenant == null)
            {
                throw new ArgumentException("Transaction tenant cannot be null.");
            }

            return Customer.BuildRowKey(transaction.GetCustomer());
        }

        protected override string BuildRowKey(Transaction transaction)
            => Order.BuildRowKey(transaction.GetOrder());
    }
}
