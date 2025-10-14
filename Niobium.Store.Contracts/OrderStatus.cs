namespace Niobium.Store
{
    public enum OrderStatus : int
    {
        Created = 0,
        PartiallyPaid = 10,
        Paid = 20,
        Shipped = 30,
        Delivered = 40,
        Completed = 50,
        Cancelled = 60,
        Refunded = 70
    }
}
