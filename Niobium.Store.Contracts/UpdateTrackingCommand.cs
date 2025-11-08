using Niobium.Messaging;

namespace Niobium.Store
{
    public class UpdateTrackingCommand : DomainEvent
    {
        public required Guid Customer { get; set; }

        public required long Order { get; init; }

        public required OrderStatus Status { get; init; }
    }
}
