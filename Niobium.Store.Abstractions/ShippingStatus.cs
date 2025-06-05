namespace Niobium.Store
{
    public enum ShippingStatus : int
    {
        Pending = 0,
        Processed = 1,
        Shipped = 2,
        Customs = 3,
        Delivering = 4,
        DeliverAttemptFailed = 5,
        Delivered = 6,
        Returned = 7,
        Cancelled = 8
    }
}
