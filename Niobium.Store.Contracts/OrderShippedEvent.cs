using Niobium.Messaging;

namespace Niobium.Store
{
    public class OrderShippedEvent : DomainEvent
    {
        public required Order Order { get; init; }
    }
}
