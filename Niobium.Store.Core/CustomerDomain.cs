using Cod;
using Cod.Finance;
using Cod.Platform;
using Cod.Platform.Finance;
using Microsoft.Extensions.Logging;

namespace Niobium.Store
{
    public class CustomerDomain(
        Lazy<IRepository<Customer>> repo,
        IEnumerable<IDomainEventHandler<IDomain<Customer>>> eventHandlers,
        Lazy<IQueryableRepository<Transaction>> transactionRepo,
        Lazy<IQueryableRepository<Accounting>> accountingRepo,
        Lazy<IEnumerable<IAccountingAuditor>> auditors,
        Lazy<ICacheStore> cacheStore,
        IDomainRepository<OrderDomain, Order> orderRepo,
        ILogger<CustomerDomain> logger)
         : AccountableDomain<Customer>(repo, eventHandlers, transactionRepo, accountingRepo, auditors, cacheStore, logger)
    {
        private const string OrderSettlementRemark = "OrderSettlement";

        public override string AccountingPrincipal => this.RowKey;

        public async Task<Customer> CreateCustomerIfNotExistAsync(Customer customer, CancellationToken cancellationToken)
        {
            var existingCustomer = await this.GetEntityAsync(cancellationToken);
            if (existingCustomer != null)
            {
                Logger.LogInformation($"Customer {this.RowKey} already exists. No action taken.");
                return existingCustomer;
            }

            var result = await this.Repository.CreateAsync(customer, cancellationToken: cancellationToken);
            await this.InitializeBalanceAsync(cancellationToken);
            await this.InitializeDeltaAsync(cancellationToken);

            return result;
        }

        public async Task<bool> SettleAsync(long order, CancellationToken cancellationToken = default)
        {
            var orderDomain = await orderRepo.GetAsync(Order.BuildPartitionKey(Customer.ParseID(this.RowKey)), Order.BuildRowKey(order), cancellationToken: cancellationToken);
            var due = await orderDomain.FigureDueAsync(cancellationToken);

            var fullID = new StorageKey { PartitionKey = orderDomain.PartitionKey, RowKey = orderDomain.RowKey };
            var balance = await this.GetBalanceAsync(DateTimeOffset.UtcNow, cancellationToken);
            if (balance.Available < due.Cents)
            {
                var error = $"Insufficient balance to settle order {fullID}. Required: {due}, Available: {balance.Available}";
                Logger.LogWarning(error);
                return false; // Not enough balance to settle the order.
            }

            await this.FreezeAsync(due.Cents);
            Logger.LogInformation($"Settling order {fullID} with amount {due} , current balance available:  {balance.Available}");

            IEnumerable<Transaction> transactions = await this.MakeTransactionAsync(-due.Cents, (int)TransactionReason.Spend, OrderSettlementRemark, fullID.ToString());
            var result = await orderDomain.PayAsync(transactions.Single(), cancellationToken);
            Logger.LogInformation($"Order {fullID} settled by transaction: {transactions.Single().GetID()}");

            await this.UnfreezeAsync(due.Cents);
            return result;
        }
    }
}
