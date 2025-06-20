using System.Diagnostics.CodeAnalysis;

namespace Niobium.Store
{
    [method: SetsRequiredMembers]
    public class OrderResponse() : Order
    {
        public required long Order { get; set; }

        public required string Instruction { get; set; }
    }
}
