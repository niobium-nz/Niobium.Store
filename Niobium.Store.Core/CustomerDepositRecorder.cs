using Cod;
using Cod.Platform.Finance;

namespace Niobium.Store
{
    internal class CustomerDepositRecorder(IDomainRepository<CustomerDomain, Customer> repo)
        : AccountDepositRecorder<CustomerDomain, Customer>(repo)
    {
        protected override string BuildPartitionKey(Transaction transaction)
            => Customer.BuildPartitionKey(transaction.GetCustomer());

        protected override string BuildRowKey(Transaction transaction)
            => Customer.BuildRowKey(transaction.GetCustomer());
    }
}
