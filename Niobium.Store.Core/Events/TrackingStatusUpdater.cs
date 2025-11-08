using Niobium.Store.Domains;

namespace Niobium.Store.Events
{
    internal class TrackingStatusUpdater(IDomainRepository<OrderDomain, Order> repo) : DomainEventHandler<IDomain<Order>, UpdateTrackingCommand>
    {
        public override async Task HandleCoreAsync(UpdateTrackingCommand e, CancellationToken cancellationToken = default)
        {
            var domain = await repo.GetAsync(Order.BuildPartitionKey(e.Customer), Order.BuildRowKey(e.Order), cancellationToken: cancellationToken);
            await domain.UpdateTrackingAsync(e.ShippingStatus, cancellationToken);
        }
    }
}
