using Cod;
using Cod.Platform.Finance;

namespace Niobium.Store
{
    internal class OrderSettler(IDomainRepository<CustomerDomain, Customer> cusomterRepo)
        : DomainEventHandler<IDomain<Transaction>, TransactionCreatedEvent>
    {
        public override async Task HandleAsync(TransactionCreatedEvent e, CancellationToken? cancellationToken = null)
        {
            var customerID = e.Transaction.GetCustomer();
            var orderID = e.Transaction.GetOrder();
            var customerDomain = await cusomterRepo.GetAsync(
                Customer.BuildPartitionKey(customerID), 
                Customer.BuildRowKey(customerID), 
                cancellationToken: cancellationToken);
            await customerDomain.SettleAsync(orderID, cancellationToken ?? CancellationToken.None);
        }
    }
}
