using Cod.Messaging;

namespace Niobium.Store
{
    public class OrderCreatedEvent : DomainEvent
    {
        public required Order NewOrder { get; init; }
    }
}
