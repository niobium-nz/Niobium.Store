namespace Niobium.Store
{
    public enum ShippingStatus : int
    {
        NotApplicable = 0,
        Pending = 1,
        Processed = 2,
        Shipped = 3,
        Customs = 4,
        Delivering = 5,
        DeliverAttemptFailed = 6,
        Delivered = 7,
        Returned = 8,
        Cancelled = 9
    }
}
