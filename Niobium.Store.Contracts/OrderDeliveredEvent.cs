using Niobium.Messaging;

namespace Niobium.Store
{
    public class OrderDeliveredEvent : DomainEvent
    {
        public required Order Order { get; init; }
    }
}
