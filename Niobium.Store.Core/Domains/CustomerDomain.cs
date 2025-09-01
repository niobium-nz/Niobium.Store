using Microsoft.Extensions.Logging;
using Niobium.Finance;
using Niobium.Platform;
using Niobium.Platform.Finance;

namespace Niobium.Store.Domains
{
    internal class CustomerDomain(
        Lazy<IRepository<Customer>> repo,
        IEnumerable<IDomainEventHandler<IDomain<Customer>>> eventHandlers,
        Lazy<IQueryableRepository<Transaction>> transactionRepo,
        Lazy<IQueryableRepository<Accounting>> accountingRepo,
        Lazy<IEnumerable<IAccountingAuditor>> auditors,
        Lazy<ICacheStore> cacheStore,
        ILogger<CustomerDomain> logger)
         : AccountableDomain<Customer>(repo, eventHandlers, transactionRepo, accountingRepo, auditors, cacheStore, logger)
    {
        private const string OrderSettlementRemark = "OrderSettlement";

        public override string? Tenant => this.PartitionKey;

        public override string AccountingPrincipal => this.RowKey!;

        public async Task<Customer> CreateCustomerIfNotExistAsync(Customer customer, CancellationToken cancellationToken)
        {
            var existingCustomer = await this.TryGetEntityAsync(cancellationToken);
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

        public async Task<Transaction?> BeginSettlementAsync(Amount due, CancellationToken cancellationToken = default)
        {
            this.CheckInitialized();
            var fullID = new StorageKey(this.PartitionKey, this.RowKey);
            var balance = await this.GetBalanceAsync(DateTimeOffset.UtcNow, cancellationToken);
            if (balance.Available < due.Cents)
            {
                var error = $"Insufficient balance to settle order {fullID}. Required: {due}, Available: {balance.Available}";
                this.Logger.LogWarning(error);
                return null; // Not enough balance to settle the order.
            }

            _ = await this.FreezeAsync(due.Cents, cancellationToken);
            this.Logger.LogInformation($"Settling order {fullID} with amount {due} , current balance available:  {balance.Available}");

            var transactions = await this.MakeTransactionAsync(-due.Cents, (int)TransactionReason.Spend, OrderSettlementRemark, fullID.ToString());
            return transactions.Single();
        }

        public async Task FinishSettlementAsync(Amount due, CancellationToken cancellationToken = default) => await this.UnfreezeAsync(due.Cents, cancellationToken);
    }
}
