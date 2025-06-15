using Cod.Messaging;

namespace Niobium.Store
{
    public class OrderCreatedEvent : DomainEvent
    {
        public OrderCreatedEvent()
        {
            this.Source = Cod.DomainEventAudience.Internal;
            this.Target = Cod.DomainEventAudience.External;
        }

        public Order Order { get; set; }
    }
}
