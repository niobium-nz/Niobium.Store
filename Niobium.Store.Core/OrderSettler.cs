using Cod;
using Cod.Finance;
using Cod.Platform.Finance;

namespace Niobium.Store
{
    internal class OrderSettler(IDomainRepository<CustomerDomain, Customer> cusomterRepo)
        : DomainEventHandler<IDomain<Transaction>, TransactionCreatedEvent>
    {
        public override async Task HandleCoreAsync(TransactionCreatedEvent e, CancellationToken cancellationToken = default)
        {
            var customerID = e.Transaction.GetCustomer();
            var orderID = e.Transaction.GetOrder();
            var customerDomain = await cusomterRepo.GetAsync(
                Customer.BuildPartitionKey(customerID), 
                Customer.BuildRowKey(customerID), 
                cancellationToken: cancellationToken);
            await customerDomain.SettleAsync(orderID, cancellationToken);
        }
    }
}
