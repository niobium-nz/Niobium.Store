using Cod;

namespace Niobium.Store
{
    internal class CustomerCreator(IDomainRepository<CustomerDomain, Customer> cusomterRepo)
        : DomainEventHandler<IDomain<Order>, EntityChangedEvent<Order>>
    {
        public override async Task HandleCoreAsync(EntityChangedEvent<Order> e, CancellationToken cancellationToken = default)
        {
            if (e.ChangeType != EntityChangeType.Created || e.Entity == null || e.Entity.Status != (int)OrderStatus.Created)
            {
                return;
            }

            var customerID = e.Entity.Customer;
            Customer customer = new()
            {
                BillingAddressLine1 = e.Entity.BillingAddressLine1,
                BillingAddressLine2 = e.Entity.BillingAddressLine2,
                BillingCity = e.Entity.BillingCity,
                BillingCountry = e.Entity.BillingCountry,
                BillingName = e.Entity.BillingName,
                BillingBusiness = e.Entity.BillingBusiness,
                BillingPostcode = e.Entity.BillingPostcode,
                BillingState = e.Entity.BillingState,
                Consignee = e.Entity.Consignee,
                Culture = e.Entity.Culture,
                Currency = e.Entity.Currency,
                Email = e.Entity.Email,
                ID = customerID,
                Tenant = Customer.BuildPartitionKey(e.Entity.Tenant),
                ShippingAddressLine1 = e.Entity.ShippingAddressLine1,
                ShippingAddressLine2 = e.Entity.ShippingAddressLine2,
                ShippingCity = e.Entity.ShippingCity,
                ShippingCountry = e.Entity.ShippingCountry,
                ShippingPostcode = e.Entity.ShippingPostcode,
                ShippingState = e.Entity.ShippingState,
                ShippingSuburb = e.Entity.ShippingSuburb,
                TimeZone = e.Entity.TimeZone,
                Phone = e.Entity.Phone,
            };

            var customerDomain = await cusomterRepo.GetAsync(
                Customer.BuildPartitionKey(e.Entity.Tenant),
                Customer.BuildRowKey(customerID),
                cancellationToken: cancellationToken);
            await customerDomain.CreateCustomerIfNotExistAsync(customer, cancellationToken);
        }
    }
}
