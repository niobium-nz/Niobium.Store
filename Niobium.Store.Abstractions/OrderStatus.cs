namespace Niobium.Store
{
    public enum OrderStatus : int
    {
        Created = 0,
        Paid = 1,
        Shipped = 2,
        Completed = 3,
        Cancelled = 4,
        Refunded = 5
    }
}
