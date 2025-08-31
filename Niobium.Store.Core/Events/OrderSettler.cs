using Niobium.Finance;
using Niobium.Store.Flows;

namespace Niobium.Store.Events
{
    internal class OrderSettler(SettleFlow flow) : DomainEventHandler<IDomain<Transaction>, TransactionCreatedEvent>
    {
        public override async Task HandleCoreAsync(TransactionCreatedEvent e, CancellationToken cancellationToken = default)
            => await flow.RunAsync(e.Transaction, cancellationToken);
    }
}
