using Cod.Messaging;

namespace Niobium.Store
{
    public class OrderCreatedEvent(Order newOrder) : DomainEvent
    {
        public Order Order { get; } = newOrder;
    }
}
