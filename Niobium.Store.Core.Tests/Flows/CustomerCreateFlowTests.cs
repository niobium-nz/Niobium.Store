using System.Reflection;
using Azure;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Niobium.Finance;
using Niobium.Platform;
using Niobium.Platform.Finance;
using Niobium.Store.Domains;
using Niobium.Store.Flows;

namespace Niobium.Store.Core.Tests.Flows
{
    // Purpose
    // Business-focused tests for CustomerCreateFlow. Keep the real CustomerDomain in scope and mock only infrastructure
    // boundaries (repositories, logger, cache). Tests are readable, junior-friendly, and scenario-driven.
    [TestClass]
    public class CustomerCreateFlowTests
    {
        // Scenario: New shopper places a first order
        // Given: A first order with full billing/shipping/contact details
        // When: CustomerCreateFlow runs for the order
        // Then: A new customer is created with fields mapped from the order and an ownership record is stored
        [TestMethod]
        public async Task Creating_new_customer_from_order_creates_customer_and_records_ownership()
        {
            // Given
            var order = BuildOrder(
                tenant: Guid.NewGuid(),
                customer: Guid.NewGuid(),
                billingName: "Alice",
                email: "alice@example.com",
                phone: "+1-555-0100",
                billing: ("10 Queen St", null, "Auckland", null, "NZ", "1010"),
                shipping: ("20 King St", null, "Auckland", null, "NZ", "1010"));

            var expectedCustomer = BuildCustomerFromOrder(order);

            // Real domain (initialized), infra mocked
            var customerRepo = new Mock<IRepository<Customer>>(MockBehavior.Strict);
            _ = customerRepo
                .Setup(r => r.CreateAsync(
                    It.Is<IEnumerable<Customer>>(l => l.Count() == 1 && MatchesCustomer(l.Single(), expectedCustomer)),
                    It.IsAny<bool>(),
                    It.IsAny<DateTimeOffset?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((IEnumerable<Customer> c, bool _, DateTimeOffset? __, CancellationToken ___) => c);

            var domain = BuildInitializedCustomerDomain(expectedCustomer, customerRepo, out _);

            var domainRepo = new Mock<IDomainRepository<CustomerDomain, Customer>>(MockBehavior.Strict);
            _ = domainRepo
                .Setup(r => r.GetAsync(
                    Customer.BuildPartitionKey(order.Tenant),
                    Customer.BuildRowKey(order.Customer),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(domain);

            var ownershipRepo = new Mock<IRepository<Ownership>>(MockBehavior.Strict);
            _ = ownershipRepo
                .Setup(r => r.CreateAsync(
                    It.Is<IEnumerable<Ownership>>(l => l.Count() == 1 && l.Single().Email == order.Email && l.Single().Order == order.GetID() && l.Single().Tenant == order.Tenant),
                    true,
                    It.IsAny<DateTimeOffset?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((IEnumerable<Ownership> o, bool _, DateTimeOffset? __, CancellationToken ___) => o);

            var flow = new CustomerCreateFlow(domainRepo.Object, ownershipRepo.Object);

            // When
            await flow.RunAsync(order, CancellationToken.None);

            // Then
            customerRepo.Verify(r => r.CreateAsync(
                It.Is<IEnumerable<Customer>>(l => l.Count() == 1 && MatchesCustomer(l.Single(), expectedCustomer)),
                It.IsAny<bool>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<CancellationToken>()),
                Times.Once);
            ownershipRepo.Verify(r => r.CreateAsync(It.IsAny<IEnumerable<Ownership>>(), true, It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        // Scenario: Returning shopper places another order
        // Given: The customer already exists in the store
        // When: CustomerCreateFlow runs for the order
        // Then: The operation is idempotent (conflict handled) and ownership is still recorded
        [TestMethod]
        public async Task Existing_customer_from_order_is_idempotent_and_records_ownership()
        {
            var order = BuildOrder(
                tenant: Guid.NewGuid(),
                customer: Guid.NewGuid(),
                billingName: "Bob",
                email: "bob@example.com",
                phone: "+1-555-0101");

            var expectedCustomer = BuildCustomerFromOrder(order);

            var customerRepo = new Mock<IRepository<Customer>>(MockBehavior.Strict);
            _ = customerRepo
                .Setup(r => r.CreateAsync(It.IsAny<IEnumerable<Customer>>(), It.IsAny<bool>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new RequestFailedException(status: 409, message: "conflict"));
            // When conflict occurs, domain fetches existing entity; allow a Get by keys to return expected
            _ = customerRepo
                .Setup(r => r.RetrieveAsync(
                    Customer.BuildPartitionKey(order.Tenant),
                    Customer.BuildRowKey(order.Customer),
                    It.IsAny<IList<string>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedCustomer);

            var domain = BuildInitializedCustomerDomain(expectedCustomer, customerRepo, out _);

            var domainRepo = new Mock<IDomainRepository<CustomerDomain, Customer>>(MockBehavior.Strict);
            _ = domainRepo
                .Setup(r => r.GetAsync(
                    Customer.BuildPartitionKey(order.Tenant),
                    Customer.BuildRowKey(order.Customer),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(domain);

            var ownershipRepo = new Mock<IRepository<Ownership>>(MockBehavior.Strict);
            _ = ownershipRepo
                .Setup(r => r.CreateAsync(
                    It.Is<IEnumerable<Ownership>>(l => l.Count() == 1 && l.Single().Email == order.Email && l.Single().Order == order.GetID() && l.Single().Tenant == order.Tenant),
                    true,
                    It.IsAny<DateTimeOffset?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((IEnumerable<Ownership> o, bool _, DateTimeOffset? __, CancellationToken ___) => o);

            var flow = new CustomerCreateFlow(domainRepo.Object, ownershipRepo.Object);

            // When
            await flow.RunAsync(order, CancellationToken.None);

            // Then
            customerRepo.Verify(r => r.CreateAsync(It.IsAny<IEnumerable<Customer>>(), It.IsAny<bool>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()), Times.Once);
            ownershipRepo.Verify(r => r.CreateAsync(It.IsAny<IEnumerable<Ownership>>(), true, It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        // Scenario: Explicit customer onboarding (no order)
        // Given: A customer profile to be onboarded
        // When: CustomerCreateFlow runs for the customer directly
        // Then: Customer is created if missing and no ownership record is written
        [TestMethod]
        public async Task Creating_customer_directly_creates_customer_no_ownership()
        {
            var tenant = Guid.NewGuid();
            var id = Guid.NewGuid();
            var customer = new Customer
            {
                Tenant = tenant,
                ID = id,
                Email = "carol@example.com",
                Consignee = "Carol",
                Currency = "USD",
                Culture = "en-US",
                TimeZone = "UTC",
                BillingName = "Carol",
                BillingAddressLine1 = "1 Main",
                BillingCity = "City",
                BillingCountry = "US",
                BillingPostcode = "10001",
                ShippingAddressLine1 = "1 Main",
                ShippingCity = "City",
                ShippingCountry = "US",
                ShippingPostcode = "10001",
            };

            var customerRepo = new Mock<IRepository<Customer>>(MockBehavior.Strict);
            _ = customerRepo
                .Setup(r => r.CreateAsync(
                    It.Is<IEnumerable<Customer>>(l => l.Count() == 1 && l.Single().Tenant == tenant && l.Single().ID == id),
                    It.IsAny<bool>(),
                    It.IsAny<DateTimeOffset?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((IEnumerable<Customer> c, bool _, DateTimeOffset? __, CancellationToken ___) => c);

            var domain = BuildInitializedCustomerDomain(customer, customerRepo, out _);

            var domainRepo = new Mock<IDomainRepository<CustomerDomain, Customer>>(MockBehavior.Strict);
            _ = domainRepo
                .Setup(r => r.GetAsync(
                    Customer.BuildPartitionKey(tenant),
                    Customer.BuildRowKey(id),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(domain);

            var ownershipRepo = new Mock<IRepository<Ownership>>(MockBehavior.Strict);

            var flow = new CustomerCreateFlow(domainRepo.Object, ownershipRepo.Object);

            // When
            await flow.RunAsync(customer, CancellationToken.None);

            // Then
            customerRepo.Verify(r => r.CreateAsync(It.IsAny<IEnumerable<Customer>>(), It.IsAny<bool>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()), Times.Once);
            ownershipRepo.VerifyNoOtherCalls();
        }

        // Scenario: Mapping from order to customer is accurate
        // Given: An order with comprehensive billing, shipping, and contact fields
        // When: CustomerCreateFlow runs for the order
        // Then: The created customer fields match the order values
        [TestMethod]
        public async Task Mapping_from_order_to_customer_is_correct()
        {
            var order = BuildOrder(
                tenant: Guid.NewGuid(),
                customer: Guid.NewGuid(),
                billingName: "Dora",
                email: "dora@example.com",
                phone: "+1-555-0102",
                billing: ("10 Queen St", "Apt 1", "Auckland", "AUK", "NZ", "1010"),
                shipping: ("20 King St", "Lvl 2", "Auckland", "AUK", "NZ", "1020"));

            var expected = BuildCustomerFromOrder(order);

            var captured = default(Customer);
            var customerRepo = new Mock<IRepository<Customer>>(MockBehavior.Strict);
            _ = customerRepo
                .Setup(r => r.CreateAsync(It.IsAny<IEnumerable<Customer>>(), It.IsAny<bool>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
                .Callback<IEnumerable<Customer>, bool, DateTimeOffset?, CancellationToken>((list, _, __, ___) => captured = list.Single())
                .ReturnsAsync((IEnumerable<Customer> list, bool _, DateTimeOffset? __, CancellationToken ___) => list);

            var domain = BuildInitializedCustomerDomain(expected, customerRepo, out _);

            var domainRepo = new Mock<IDomainRepository<CustomerDomain, Customer>>(MockBehavior.Strict);
            _ = domainRepo
                .Setup(r => r.GetAsync(
                    Customer.BuildPartitionKey(order.Tenant),
                    Customer.BuildRowKey(order.Customer),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(domain);

            var ownershipRepo = new Mock<IRepository<Ownership>>(MockBehavior.Strict);
            _ = ownershipRepo
                .Setup(r => r.CreateAsync(It.IsAny<IEnumerable<Ownership>>(), true, It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IEnumerable<Ownership> o, bool _, DateTimeOffset? __, CancellationToken ___) => o);

            var flow = new CustomerCreateFlow(domainRepo.Object, ownershipRepo.Object);

            // When
            await flow.RunAsync(order, CancellationToken.None);

            // Then
            _ = captured.Should().NotBeNull();
            _ = MatchesCustomer(captured!, expected).Should().BeTrue();
        }

        // Scenario: Repository scoping keys are correct for resolving CustomerDomain
        // Given: Either order-driven or direct customer-driven creation
        // When: The flow resolves the CustomerDomain from the repo
        // Then: It uses tenant and customer IDs to build the correct keys
        [TestMethod]
        public async Task Repository_scoping_keys_are_correct_for_customer_resolution()
        {
            var tenant = Guid.NewGuid();
            var id = Guid.NewGuid();
            var order = BuildOrder(tenant, id, billingName: "Eve", email: "eve@example.com");
            var expected = BuildCustomerFromOrder(order);

            var customerRepo = new Mock<IRepository<Customer>>(MockBehavior.Loose);
            // Ensure create returns the passed customer so CreateCustomerIfNotExistAsync doesn't fail
            _ = customerRepo
                .Setup(r => r.CreateAsync(
                    It.IsAny<IEnumerable<Customer>>(),
                    It.IsAny<bool>(),
                    It.IsAny<DateTimeOffset?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((IEnumerable<Customer> c, bool _, DateTimeOffset? __, CancellationToken ___) => c);

            var domain = BuildInitializedCustomerDomain(expected, customerRepo, out _);

            var domainRepo = new Mock<IDomainRepository<CustomerDomain, Customer>>(MockBehavior.Strict);
            _ = domainRepo
                .Setup(r => r.GetAsync(
                    Customer.BuildPartitionKey(tenant),
                    Customer.BuildRowKey(id),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(domain);

            var ownershipRepo = new Mock<IRepository<Ownership>>(MockBehavior.Loose);
            // Ensure ownership create returns the passed ownership so extension method doesn't fail
            _ = ownershipRepo
                .Setup(r => r.CreateAsync(
                    It.IsAny<IEnumerable<Ownership>>(),
                    It.IsAny<bool>(),
                    It.IsAny<DateTimeOffset?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((IEnumerable<Ownership> o, bool _, DateTimeOffset? __, CancellationToken ___) => o);

            var flow = new CustomerCreateFlow(domainRepo.Object, ownershipRepo.Object);

            // Act
            await flow.RunAsync(order, CancellationToken.None);

            // Assert
            domainRepo.Verify(r => r.GetAsync(Customer.BuildPartitionKey(tenant), Customer.BuildRowKey(id), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        // Scenario: Ownership write fails (transient infra issue)
        // Given: A normal order
        // When: Ownership repository fails to write
        // Then: The business call bubbles the error so the caller can retry
        [TestMethod]
        public async Task Ownership_write_failure_bubbles_up()
        {
            var order = BuildOrder(
                tenant: Guid.NewGuid(),
                customer: Guid.NewGuid(),
                billingName: "Frank",
                email: "frank@example.com");

            var expectedCustomer = BuildCustomerFromOrder(order);

            var customerRepo = new Mock<IRepository<Customer>>(MockBehavior.Strict);
            _ = customerRepo
                .Setup(r => r.CreateAsync(It.IsAny<IEnumerable<Customer>>(), It.IsAny<bool>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IEnumerable<Customer> c, bool _, DateTimeOffset? __, CancellationToken ___) => c);

            var domain = BuildInitializedCustomerDomain(expectedCustomer, customerRepo, out _);

            var domainRepo = new Mock<IDomainRepository<CustomerDomain, Customer>>(MockBehavior.Strict);
            _ = domainRepo
                .Setup(r => r.GetAsync(
                    Customer.BuildPartitionKey(order.Tenant),
                    Customer.BuildRowKey(order.Customer),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(domain);

            var ownershipRepo = new Mock<IRepository<Ownership>>(MockBehavior.Strict);
            _ = ownershipRepo
                .Setup(r => r.CreateAsync(It.IsAny<IEnumerable<Ownership>>(), true, It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("OwnershipStoreUnavailable"));

            var flow = new CustomerCreateFlow(domainRepo.Object, ownershipRepo.Object);

            // Act
            Func<Task> act = async () => await flow.RunAsync(order, CancellationToken.None);

            // Assert
            _ = await act.Should().ThrowAsync<Exception>();
        }

        // --- Builders & helpers ---
        private static Order BuildOrder(
            Guid tenant,
            Guid customer,
            string billingName,
            string email,
            string? phone = null,
            (string line1, string? line2, string city, string? state, string country, string postcode)? billing = null,
            (string line1, string? line2, string city, string? state, string country, string postcode)? shipping = null)
        {
            var created = DateTimeOffset.UtcNow.AddMinutes(-5);
            var order = new Order
            {
                Customer = customer,
                Created = created,
                Status = (int)OrderStatus.Created,
                ShippingStatus = (int)ShippingStatus.NotApplicable,
                Tenant = tenant,
                Currency = "USD",
                Culture = "en-US",
                TimeZone = "UTC",
                Consignee = billingName,
                Email = email,
                Phone = phone,
                ShippingAddressLine1 = shipping?.line1 ?? "123 Main St",
                ShippingAddressLine2 = shipping?.line2,
                ShippingCity = shipping?.city ?? "City",
                ShippingState = shipping?.state,
                ShippingCountry = shipping?.country ?? "US",
                ShippingPostcode = shipping?.postcode ?? "10001",
                BillingName = billingName,
                BillingAddressLine1 = billing?.line1 ?? "123 Main St",
                BillingAddressLine2 = billing?.line2,
                BillingCity = billing?.city ?? "City",
                BillingState = billing?.state,
                BillingCountry = billing?.country ?? "US",
                BillingPostcode = billing?.postcode ?? "10001",
                SubTotal = 0,
                GrandTotal = 0,
                Discount = 0,
                ShippingCost = 0,
                Tax = 0,
                Settled = 0,
                Cart = "[]",
            };
            order.SetCart(Array.Empty<CartItem>());
            return order;
        }

        private static Customer BuildCustomerFromOrder(Order order) => new()
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

        private static bool MatchesCustomer(Customer a, Customer b) => a.Tenant == b.Tenant && a.ID == b.ID &&
                   a.Email == b.Email && a.Consignee == b.Consignee &&
                   a.Currency == b.Currency && a.Culture == b.Culture && a.TimeZone == b.TimeZone && a.Phone == b.Phone &&
                   a.BillingName == b.BillingName && a.BillingBusiness == b.BillingBusiness &&
                   a.BillingAddressLine1 == b.BillingAddressLine1 && a.BillingAddressLine2 == b.BillingAddressLine2 &&
                   a.BillingCity == b.BillingCity && a.BillingState == b.BillingState && a.BillingCountry == b.BillingCountry && a.BillingPostcode == b.BillingPostcode &&
                   a.ShippingAddressLine1 == b.ShippingAddressLine1 && a.ShippingAddressLine2 == b.ShippingAddressLine2 &&
                   a.ShippingCity == b.ShippingCity && a.ShippingState == b.ShippingState && a.ShippingCountry == b.ShippingCountry && a.ShippingPostcode == b.ShippingPostcode &&
                   a.ShippingSuburb == b.ShippingSuburb;

        private static CustomerDomain BuildInitializedCustomerDomain(
            Customer seed,
            Mock<IRepository<Customer>> customerRepo,
            out Mock<ILogger<CustomerDomain>> logger)
        {
            logger = new Mock<ILogger<CustomerDomain>>(MockBehavior.Loose);
            var transactionRepo = new Mock<IQueryableRepository<Transaction>>(MockBehavior.Loose);
            var accountingRepo = new Mock<IQueryableRepository<Accounting>>(MockBehavior.Loose);
            var auditors = new List<IAccountingAuditor>();
            var cache = new Mock<ICacheStore>(MockBehavior.Loose);

            // Ensure InitializeBalanceAsync/InitializeDeltaAsync succeed by returning created accounting records
            _ = accountingRepo
                .Setup(r => r.CreateAsync(
                    It.IsAny<IEnumerable<Accounting>>(),
                    It.IsAny<bool>(),
                    It.IsAny<DateTimeOffset?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((IEnumerable<Accounting> list, bool _, DateTimeOffset? __, CancellationToken ___) => list);

            var domain = new CustomerDomain(
                new Lazy<IRepository<Customer>>(() => customerRepo.Object),
                Array.Empty<IDomainEventHandler<IDomain<Customer>>>(),
                new Lazy<IQueryableRepository<Transaction>>(() => transactionRepo.Object),
                new Lazy<IQueryableRepository<Accounting>>(() => accountingRepo.Object),
                new Lazy<IEnumerable<IAccountingAuditor>>(() => auditors),
                new Lazy<ICacheStore>(() => cache.Object),
                logger.Object);

            InitializeDomain(domain, seed);
            return domain;
        }

        private static void InitializeDomain<TDomain, TEntity>(TDomain domain, TEntity entity)
        {
            var method = GetMethodRecursive(domain!.GetType(), "Initialize", new[] { typeof(TEntity) })
                ?? throw new InvalidOperationException($"Could not find Initialize({typeof(TEntity).Name}) on {domain.GetType().Name}");
            _ = method.Invoke(domain, new object[] { entity! });
        }

        private static MethodInfo? GetMethodRecursive(Type type, string name, Type[] parameterTypes)
        {
            while (type != null)
            {
                var mi = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, binder: null, types: parameterTypes, modifiers: null);
                if (mi != null)
                {
                    return mi;
                }

                type = type.BaseType!;
            }
            return null;
        }
    }
}
