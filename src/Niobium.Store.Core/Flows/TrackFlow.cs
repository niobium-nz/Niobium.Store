namespace Niobium.Store.Flows
{
    public class TrackFlow(IRepository<Ownership> ownershipRepo, IRepository<Order> orderRepo) : IFlow
    {
        public async Task<TrackResponse> RunAsync(TrackRequest request, CancellationToken cancellationToken)
        {
            var ownership = await ownershipRepo.RetrieveAsync(
                Ownership.BuildPartitionKey(request.Email),
                Ownership.BuildRowKey(request.Order),
                cancellationToken: cancellationToken)
                ?? throw new ApplicationException(InternalError.NotFound, "Order not found.");
            var order = await orderRepo.RetrieveAsync(Order.BuildPartitionKey(ownership.Customer), Order.BuildRowKey(request.Order), cancellationToken: cancellationToken)
                ?? throw new ApplicationException(InternalError.NotFound, "Order not found.");
            return new TrackResponse
            {
                Cart = order.GetCart(),
                Created = order.Created,
                ShippingCity = order.ShippingCity,
                ShippingCountry = Country.Parse(order.ShippingCountry),
                ShippingState = order.ShippingState,
                ShippingStatus = (ShippingStatus)order.ShippingStatus,
                Status = (OrderStatus)order.Status,
            };
        }
    }
}
