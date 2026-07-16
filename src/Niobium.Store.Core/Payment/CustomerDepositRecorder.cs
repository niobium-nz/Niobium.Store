using Niobium.Finance;
using Niobium.Platform.Finance;
using Niobium.Store.Domains;

namespace Niobium.Store.Payment
{
    internal class CustomerDepositRecorder(IDomainRepository<CustomerDomain, Customer> repo)
        : AccountDepositRecorder<CustomerDomain, Customer>(repo)
    {
        protected override string BuildPartitionKey(Transaction transaction) => transaction.Tenant == null
                ? throw new ArgumentException("Transaction tenant cannot be null.")
                : Customer.BuildRowKey(transaction.GetCustomer());

        protected override string BuildRowKey(Transaction transaction)
            => Order.BuildRowKey(transaction.GetOrder());
    }
}
