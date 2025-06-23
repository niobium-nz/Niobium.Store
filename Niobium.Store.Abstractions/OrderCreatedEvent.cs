using Cod.Messaging;

namespace Niobium.Store
{
    public class OrderCreatedEvent : DomainEvent
    {
        public required Order Order { get; init; }
    }
}
