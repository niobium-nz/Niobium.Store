using Niobium.Messaging;

namespace Niobium.Store
{
    public class UpdateTrackingCommand : DomainEvent
    {
        public required Guid Tenant { get; init; }

        public required Guid Customer { get; set; }

        public required long Order { get; init; }

        public required OrderStatus ShippingStatus { get; init; }
    }
}
