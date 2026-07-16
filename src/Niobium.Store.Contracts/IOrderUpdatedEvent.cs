namespace Niobium.Store
{
    public interface IOrderUpdatedEvent : IDomainEvent
    {
        Order Order { get; }
    }
}
