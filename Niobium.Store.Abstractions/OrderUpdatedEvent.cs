using Cod.Messaging;

namespace Niobium.Store
{
    public class OrderUpdatedEvent : DomainEvent
    {
        public Order Order { get; set; }
    }
}
