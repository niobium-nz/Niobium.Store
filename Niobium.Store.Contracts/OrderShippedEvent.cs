using Niobium.Messaging;

namespace Niobium.Store
{
    public class OrderShippedEvent : DomainEvent, IOrderUpdatedEvent
    {
        public required Order Order { get; init; }
    }
}
