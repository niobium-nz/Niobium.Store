using Cod;

namespace Niobium.Store
{
    internal class CustomerCreator(IDomainRepository<CustomerDomain, Customer> cusomterRepo)
        : DomainEventHandler<IDomain<Order>, OrderUpdatedEvent>
    {
        protected override DomainEventAudience EventSource => DomainEventAudience.External;

        public override async Task HandleCoreAsync(OrderUpdatedEvent e, CancellationToken cancellationToken = default)
        {
            if (e.Order.Status != (int)OrderStatus.Created)
            {
                return;
            }

            var customerID = e.Order.Customer;
            Customer customer = new()
            {
                BillingAddressLine1 = e.Order.BillingAddressLine1,
                BillingAddressLine2 = e.Order.BillingAddressLine2,
                BillingCity = e.Order.BillingCity,
                BillingCountry = e.Order.BillingCountry,
                BillingName = e.Order.BillingName,
                BillingBusiness = e.Order.BillingBusiness,
                BillingPostcode = e.Order.BillingPostcode,
                BillingState = e.Order.BillingState,
                Consignee = e.Order.Consignee,
                Culture = e.Order.Culture,
                Currency = e.Order.Currency,
                Email = e.Order.Email,
                ID = customerID,
                Prefix = Customer.BuildPartitionKey(customerID),
                ShippingAddressLine1 = e.Order.ShippingAddressLine1,
                ShippingAddressLine2 = e.Order.ShippingAddressLine2,
                ShippingCity = e.Order.ShippingCity,
                ShippingCountry = e.Order.ShippingCountry,
                ShippingPostcode = e.Order.ShippingPostcode,
                ShippingState = e.Order.ShippingState,
                ShippingSuburb = e.Order.ShippingSuburb,
                TimeZone = e.Order.TimeZone,
                Phone = e.Order.Phone,
            };

            var customerDomain = await cusomterRepo.GetAsync(
                Customer.BuildPartitionKey(customerID),
                Customer.BuildRowKey(customerID),
                cancellationToken: cancellationToken);
            await customerDomain.CreateCustomerIfNotExistAsync(customer, cancellationToken);
        }
    }
}
