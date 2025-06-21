using Cod;

namespace Niobium.Store
{
    internal class CustomerCreator(IDomainRepository<CustomerDomain, Customer> cusomterRepo)
        : DomainEventHandler<IDomain<Order>, EntityChangedEvent<Order>>
    {
        public override async Task HandleCoreAsync(EntityChangedEvent<Order> e, CancellationToken cancellationToken = default)
        {
            if (e.OldEntity != null || e.NewEntity == null || e.NewEntity.Status != (int)OrderStatus.Created)
            {
                return;
            }

            var customerID = e.NewEntity.Customer;
            Customer customer = new()
            {
                BillingAddressLine1 = e.NewEntity.BillingAddressLine1,
                BillingAddressLine2 = e.NewEntity.BillingAddressLine2,
                BillingCity = e.NewEntity.BillingCity,
                BillingCountry = e.NewEntity.BillingCountry,
                BillingName = e.NewEntity.BillingName,
                BillingBusiness = e.NewEntity.BillingBusiness,
                BillingPostcode = e.NewEntity.BillingPostcode,
                BillingState = e.NewEntity.BillingState,
                Consignee = e.NewEntity.Consignee,
                Culture = e.NewEntity.Culture,
                Currency = e.NewEntity.Currency,
                Email = e.NewEntity.Email,
                ID = customerID,
                Prefix = Customer.BuildPartitionKey(customerID),
                ShippingAddressLine1 = e.NewEntity.ShippingAddressLine1,
                ShippingAddressLine2 = e.NewEntity.ShippingAddressLine2,
                ShippingCity = e.NewEntity.ShippingCity,
                ShippingCountry = e.NewEntity.ShippingCountry,
                ShippingPostcode = e.NewEntity.ShippingPostcode,
                ShippingState = e.NewEntity.ShippingState,
                ShippingSuburb = e.NewEntity.ShippingSuburb,
                TimeZone = e.NewEntity.TimeZone,
                Phone = e.NewEntity.Phone,
            };

            var customerDomain = await cusomterRepo.GetAsync(
                Customer.BuildPartitionKey(customerID),
                Customer.BuildRowKey(customerID),
                cancellationToken: cancellationToken);
            await customerDomain.CreateCustomerIfNotExistAsync(customer, cancellationToken);
        }
    }
}
