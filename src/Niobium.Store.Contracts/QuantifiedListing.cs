using System.Diagnostics.CodeAnalysis;

namespace Niobium.Store
{
    [method: SetsRequiredMembers]
    public class QuantifiedListing() : Listing
    {
        public required int Quantity { get; set; }
    }
}
