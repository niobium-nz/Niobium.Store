using Niobium;
using Niobium.Finance;
using Niobium.Platform;
using Niobium.Platform.Finance;
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

        public override string? Tenant => this.PartitionKey;

        public override string AccountingPrincipal => this.RowKey!;

        public async Task<Customer> CreateCustomerIfNotExistAsync(Customer customer, CancellationToken cancellationToken)
        {
            var existingCustomer = await this.GetEntityAsync(cancellationToken);
            if (existingCustomer != null)
            {
                this.Logger.LogInformation($"Customer {this.RowKey} already exists. No action taken.");
                return existingCustomer;
            }

            var result = await this.Repository.CreateAsync(customer, cancellationToken: cancellationToken);
            await this.InitializeBalanceAsync(cancellationToken);
            await this.InitializeDeltaAsync(cancellationToken);

            return result;
        }

        public async Task<bool> SettleAsync(long order, CancellationToken cancellationToken = default)
        {
            this.CheckInitialized();
            var orderDomain = await orderRepo.GetAsync(Order.BuildPartitionKey(Customer.ParseID(this.RowKey)), Order.BuildRowKey(order), cancellationToken: cancellationToken);
            if (!orderDomain.Initialized)
            {
                throw new ApplicationException(InternalError.NotFound);
            }

            var orderEntity = await orderDomain.GetEntityAsync(cancellationToken);
            if (orderEntity.Status >= (int)OrderStatus.Paid)
            {
                return true; // Order is already settled or paid, nothing to do.
            }

            var due = await orderDomain.FigureDueAsync(cancellationToken);
            if (due.Cents <= 0)
            {
                return true; // No due amount to settle, nothing to do.
            }

            var fullID = new StorageKey { PartitionKey = orderDomain.PartitionKey, RowKey = orderDomain.RowKey };
            var balance = await this.GetBalanceAsync(DateTimeOffset.UtcNow, cancellationToken);
            if (balance.Available < due.Cents)
            {
                var error = $"Insufficient balance to settle order {fullID}. Required: {due}, Available: {balance.Available}";
                this.Logger.LogWarning(error);
                return false; // Not enough balance to settle the order.
            }

            _ = await this.FreezeAsync(due.Cents);
            this.Logger.LogInformation($"Settling order {fullID} with amount {due} , current balance available:  {balance.Available}");

            var transactions = await this.MakeTransactionAsync(-due.Cents, (int)TransactionReason.Spend, OrderSettlementRemark, fullID.ToString());
            var result = await orderDomain.PayAsync(transactions.Single(), cancellationToken);
            this.Logger.LogInformation($"Order {fullID} settled by transaction: {transactions.Single().GetID()}");

            _ = await this.UnfreezeAsync(due.Cents);
            return result;
        }
    }
}
