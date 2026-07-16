using Niobium.Store.Flows;

namespace Niobium.Store.Events
{
    internal class OrderConfirmedNotifier(NotificationFlow flow) : ShippingStatusNotifier<OrderSettledEvent>(flow) { }

    internal class OrderShippedNotifier(NotificationFlow flow) : ShippingStatusNotifier<OrderShippedEvent>(flow) { }

    internal class OrderDeliveredNotifier(NotificationFlow flow) : ShippingStatusNotifier<OrderDeliveredEvent>(flow) { }

    internal abstract class ShippingStatusNotifier<T>(NotificationFlow flow) : DomainEventHandler<IDomain<Order>, T>
        where T : class, IOrderUpdatedEvent
    {
        protected override DomainEventAudience EventSource => DomainEventAudience.External;

        public override async Task HandleCoreAsync(T e, CancellationToken cancellationToken) => await flow.RunAsync(e.Order, cancellationToken);
    }
}
