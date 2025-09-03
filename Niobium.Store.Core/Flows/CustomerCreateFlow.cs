using Niobium.Store.Domains;

namespace Niobium.Store.Flows
{
    internal class CustomerCreateFlow(IDomainRepository<CustomerDomain, Customer> repo) : IFlow
    {
        public async Task RunAsync(Order order, CancellationToken cancellationToken = default)
            => await this.RunAsync(BuildCustomer(order), cancellationToken);

        public async Task RunAsync(Customer customer, CancellationToken cancellationToken = default)
        {
            var customerDomain = await repo.GetAsync(
                Customer.BuildPartitionKey(customer.Tenant),
                Customer.BuildRowKey(customer.ID),
                cancellationToken: cancellationToken);
            _ = await customerDomain.CreateCustomerIfNotExistAsync(customer, cancellationToken);
        }

        private static Customer BuildCustomer(Order order) => new()
        {
            BillingAddressLine1 = order.BillingAddressLine1,
            BillingAddressLine2 = order.BillingAddressLine2,
            BillingCity = order.BillingCity,
            BillingCountry = order.BillingCountry,
            BillingName = order.BillingName,
            BillingBusiness = order.BillingBusiness,
            BillingPostcode = order.BillingPostcode,
            BillingState = order.BillingState,
            Consignee = order.Consignee,
            Culture = order.Culture,
            Currency = order.Currency,
            Email = order.Email,
            ID = order.Customer,
            Tenant = order.Tenant,
            ShippingAddressLine1 = order.ShippingAddressLine1,
            ShippingAddressLine2 = order.ShippingAddressLine2,
            ShippingCity = order.ShippingCity,
            ShippingCountry = order.ShippingCountry,
            ShippingPostcode = order.ShippingPostcode,
            ShippingState = order.ShippingState,
            ShippingSuburb = order.ShippingSuburb,
            TimeZone = order.TimeZone,
            Phone = order.Phone,
        };
    }
}
