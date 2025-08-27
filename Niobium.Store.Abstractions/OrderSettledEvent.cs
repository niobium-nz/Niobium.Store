using Niobium.Messaging;

namespace Niobium.Store
{
    public class OrderSettledEvent : DomainEvent
    {
        public required Order Order { get; init; }
    }
}
