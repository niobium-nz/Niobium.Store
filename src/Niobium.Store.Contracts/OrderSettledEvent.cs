using Niobium.Messaging;

namespace Niobium.Store
{
    public class OrderSettledEvent : DomainEvent, IOrderUpdatedEvent
    {
        public required Order Order { get; init; }
    }
}
