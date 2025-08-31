using Niobium.Finance;
using Niobium.Store.Domains;
using Niobium.Store.Payment;

namespace Niobium.Store.Flows
{
    internal class SettleFlow(IDomainRepository<OrderDomain, Order> orderRepo, IDomainRepository<CustomerDomain, Customer> customerRepo) : IFlow
    {
        public async Task RunAsync(Transaction transaction, CancellationToken cancellationToken = default)
        {
            var customerID = transaction.GetCustomer();
            var orderID = transaction.GetOrder();
            var tenant = transaction.GetTenant();

            var orderDomain = await orderRepo.GetAsync(
                Order.BuildPartitionKey(customerID),
                Order.BuildRowKey(orderID),
                cancellationToken: cancellationToken);
            if (!orderDomain.Initialized)
            {
                throw new ApplicationException(InternalError.NotFound);
            }

            var due = await orderDomain.FigureDueAsync(cancellationToken);
            if (due.Cents <= 0)
            {
                // Nothing need to be settled.
                return;
            }

            var customerDomain = await customerRepo.GetAsync(
                Customer.BuildPartitionKey(tenant),
                Customer.BuildRowKey(customerID),
                cancellationToken: cancellationToken);

            var customerTransaction = await customerDomain.BeginSettlementAsync(due, cancellationToken);
            if (customerTransaction != null)
            {
                try
                {
                    _ = await orderDomain.SettleAsync(customerTransaction, cancellationToken);
                }
                finally
                {
                    await customerDomain.FinishSettlementAsync(due, cancellationToken);
                }
            }
        }
    }
}