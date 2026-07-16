using Niobium.Messaging;

namespace Niobium.Store
{
    public class OrderDeliveredEvent : DomainEvent, IOrderUpdatedEvent
    {
        public required Order Order { get; init; }
    }
}
