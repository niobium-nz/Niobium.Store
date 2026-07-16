namespace Niobium.Store
{
    public class TrackResponse
    {
        public required DateTimeOffset Created { get; set; }

        public OrderStatus Status { get; set; }

        public required CartItem[] Cart { get; set; }

        public ShippingStatus ShippingStatus { get; set; }

        public required string ShippingCity { get; set; }

        public string? ShippingState { get; set; }

        public required Country ShippingCountry { get; set; }
    }
}
