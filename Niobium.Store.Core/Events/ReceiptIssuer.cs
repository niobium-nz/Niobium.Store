using Niobium.Store.Flows;

namespace Niobium.Store.Events
{
    internal class ReceiptIssuer(InvoiceFlow flow)
        : DomainEventHandler<IDomain<Order>, OrderSettledEvent>
    {
        protected override DomainEventAudience EventSource => DomainEventAudience.External;

        public override async Task HandleCoreAsync(OrderSettledEvent e, CancellationToken cancellationToken)
            => await flow.RunAsync(e.Order, cancellationToken);
    }
}
