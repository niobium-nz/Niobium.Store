namespace Niobium.Store.Host
{
    internal class QueueNames
    {
        public const string OrderCreatedEvent = "ordercreatedevent";
        public const string UpdateTrackingCommand = "updatetrackingcommand";
        public const string FulfillOrderCommand = "fulfillordercommand";
        public const string OrderDeliveredEvent = "orderdeliveredevent";
        public const string OrderSettledEvent = "ordersettledevent";
        public const string OrderShippedEvent = "ordershippedevent";
    }
}
