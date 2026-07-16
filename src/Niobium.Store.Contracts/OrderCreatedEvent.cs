using Niobium.Messaging;

namespace Niobium.Store
{
    public class OrderCreatedEvent : DomainEvent, IOrderUpdatedEvent
    {
        public required Order Order { get; init; }
    }
}
