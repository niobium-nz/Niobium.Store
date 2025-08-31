using Niobium.Store.Flows;

namespace Niobium.Store.Events
{
    internal class CustomerCreator(CustomerCreateFlow flow)
        : DomainEventHandler<IDomain<Order>, EntityChangedEventArgs<Order>>
    {
        public override async Task HandleCoreAsync(EntityChangedEventArgs<Order> e, CancellationToken cancellationToken = default)
        {
            if (e.ChangeType.HasFlag(EntityChangeType.Created) && e.Entity.Status == (int)OrderStatus.Created)
            {
                await flow.RunAsync(e.Entity, cancellationToken);
            }
        }
    }
}
