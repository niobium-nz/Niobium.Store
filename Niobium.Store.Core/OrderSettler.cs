using Niobium;
using Niobium.Finance;

namespace Niobium.Store
{
    internal class OrderSettler(IDomainRepository<CustomerDomain, Customer> cusomterRepo)
        : DomainEventHandler<IDomain<Transaction>, TransactionCreatedEvent>
    {
        public override async Task HandleCoreAsync(TransactionCreatedEvent e, CancellationToken cancellationToken = default)
        {
            var customerID = e.Transaction.GetCustomer();
            var orderID = e.Transaction.GetOrder();
            var tenant = e.Transaction.GetTenant();
            var customerDomain = await cusomterRepo.GetAsync(
                Customer.BuildPartitionKey(tenant),
                Customer.BuildRowKey(customerID),
                cancellationToken: cancellationToken);
            await customerDomain.SettleAsync(orderID, cancellationToken);
        }
    }
}
