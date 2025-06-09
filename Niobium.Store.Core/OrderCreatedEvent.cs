namespace Niobium.Store
{
    public class OrderCreatedEvent(Order newOrder)
    {
        public Order Order { get; } = newOrder;
    }
}
