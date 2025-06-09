namespace Niobium.Store
{
    public enum OrderStatus : int
    {
        Created = 0,
        PartiallyPaid = 1,
        Paid = 2,
        Shipped = 3,
        Completed = 4,
        Cancelled = 5,
        Refunded = 6
    }
}
