using Niobium.Messaging;

namespace Niobium.Store
{
    public class FulfillOrderCommand : DomainEvent
    {
        public required Order Order { get; init; }

        public required List<QuantifiedListing> Items { get; init; }
    }
}
