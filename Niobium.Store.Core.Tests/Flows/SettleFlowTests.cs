using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Niobium.Finance;
using Niobium.Store.Domains;
using Niobium.Store.Flows;
using Niobium.Store.Options;

namespace Niobium.Store.Core.Tests.Flows
{
    // Purpose
    // These tests describe the business behavior of SettleFlow from a customer/accounting perspective.
    // They use real Domain logic (OrderDomain) and only mock infrastructure (repositories/loggers),
    // so the tests remain readable and aligned with real business rules (no mocking Domain methods).
    //
    // Key rules covered:
    // - A payment for a non-existent order is rejected as NotFound.
    // - If the order is already paid, nothing further happens.
    // - If the order is not in the Created state, no settlement is attempted.
    // - Malformed payment notifications are rejected upfront.
    // - Repository lookups must use the correct tenant/customer scoping keys.
    [TestClass]
    public class SettleFlowTests
    {
        // Scenario: Payment received for a non-existent order
        // Given a transaction that references an order ID that does not exist
        // When SettleFlow handles the transaction
        // Then the business flow rejects with NotFound and no customer account operations occur
        [TestMethod]
        public async Task Paying_for_a_nonexistent_order_rejects_with_not_found()
        {
            var tenant = Guid.NewGuid();
            var customerId = Guid.NewGuid();
            var orderId = 10001L;
            var tx = BuildTransaction(tenant, customerId, orderId, -500);

            // External boundary: repository returns a domain that is not initialized => order not found
            var orderRepo = new Mock<IDomainRepository<OrderDomain, Order>>(MockBehavior.Strict);
            _ = orderRepo
                .Setup(r => r.GetAsync(
                    Order.BuildPartitionKey(customerId),
                    Order.BuildRowKey(orderId),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(BuildUninitializedOrderDomain());

            var customerRepo = new Mock<IDomainRepository<CustomerDomain, Customer>>(MockBehavior.Strict);

            var flow = new SettleFlow(orderRepo.Object, customerRepo.Object);

            // Act
            Func<Task> act = async () => await flow.RunAsync(tx, CancellationToken.None);

            // Assert (business outcome)
            var ex = await act.Should().ThrowAsync<ApplicationException>();
            _ = ex.Which.ErrorCode.Should().Be(InternalError.NotFound);
            customerRepo.VerifyNoOtherCalls();
        }

        // Scenario: Payment arrives for an order that is already fully paid
        // Given an order whose GrandTotal is already Settled and status is Paid
        // When SettleFlow handles a new payment for the same order
        // Then nothing is settled again and customer account is not touched
        [TestMethod]
        public async Task Paying_for_an_already_paid_order_does_nothing()
        {
            var tenant = Guid.NewGuid();
            var customerId = Guid.NewGuid();
            var orderId = 20002L;
            var order = BuildOrder(tenant, customerId, created: DateTimeOffset.UtcNow.AddMinutes(-10),
                grandTotal: 1000, settled: 1000, status: OrderStatus.Paid);
            var orderDomain = BuildInitializedOrderDomain(order);

            // External boundary: order domain is found and initialized
            var orderRepo = new Mock<IDomainRepository<OrderDomain, Order>>(MockBehavior.Strict);
            _ = orderRepo
                .Setup(r => r.GetAsync(
                    Order.BuildPartitionKey(customerId),
                    Order.BuildRowKey(orderId),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(orderDomain);

            var customerRepo = new Mock<IDomainRepository<CustomerDomain, Customer>>(MockBehavior.Strict);

            var flow = new SettleFlow(orderRepo.Object, customerRepo.Object);

            // Act
            var tx = BuildTransaction(tenant, customerId, orderId, -1000);
            await flow.RunAsync(tx, CancellationToken.None);

            // Assert (business outcome)
            customerRepo.VerifyNoOtherCalls();
        }

        // Scenario: Order is not eligible for settlement due to its current status
        // Given an order in a terminal or non-created state (e.g., Cancelled)
        // When SettleFlow handles a payment
        // Then the flow does not attempt to settle or touch customer account
        [TestMethod]
        public async Task Paying_for_an_order_not_in_created_state_does_nothing()
        {
            var tenant = Guid.NewGuid();
            var customerId = Guid.NewGuid();
            var orderId = 30003L;
            var order = BuildOrder(tenant, customerId, DateTimeOffset.UtcNow.AddMinutes(-20),
                grandTotal: 1500, settled: 0, status: OrderStatus.Cancelled);
            var orderDomain = BuildInitializedOrderDomain(order);

            // External boundary: order domain is found and initialized
            var orderRepo = new Mock<IDomainRepository<OrderDomain, Order>>(MockBehavior.Strict);
            _ = orderRepo
                .Setup(r => r.GetAsync(
                    Order.BuildPartitionKey(customerId),
                    Order.BuildRowKey(orderId),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(orderDomain);

            var customerRepo = new Mock<IDomainRepository<CustomerDomain, Customer>>(MockBehavior.Strict);

            var flow = new SettleFlow(orderRepo.Object, customerRepo.Object);

            // Act
            var tx = BuildTransaction(tenant, customerId, orderId, -1500);
            await flow.RunAsync(tx, CancellationToken.None);

            // Assert (business outcome)
            customerRepo.VerifyNoOtherCalls();
        }

        // Scenario: Malformed payment notification is received
        // Given a transaction with invalid tenant/customer/order identifiers
        // When SettleFlow attempts to parse the transaction
        // Then it rejects the request as invalid without touching any repository
        [TestMethod]
        public async Task Payment_with_invalid_transaction_fields_is_rejected()
        {
            var tx = new Transaction
            {
                PartitionKey = "not-a-guid", // invalid customer
                Tenant = "also-not-a-guid",  // invalid tenant
                Reference = "not-a-long",    // invalid order id
                RowKey = Guid.NewGuid().ToString(),
                Delta = -1000
            };

            var orderRepo = new Mock<IDomainRepository<OrderDomain, Order>>(MockBehavior.Strict);
            var customerRepo = new Mock<IDomainRepository<CustomerDomain, Customer>>(MockBehavior.Strict);

            var flow = new SettleFlow(orderRepo.Object, customerRepo.Object);

            // Act
            Func<Task> act = async () => await flow.RunAsync(tx, CancellationToken.None);

            // Assert (business outcome)
            _ = await act.Should().ThrowAsync<InvalidOperationException>();
            orderRepo.VerifyNoOtherCalls();
            customerRepo.VerifyNoOtherCalls();
        }

        // Scenario: Repository scoping by tenant & customer is correct
        // Given an order that requires settlement (Created; due > 0)
        // When SettleFlow resolves the Order and Customer domains
        // Then it uses the tenant & customer IDs from the transaction to build the correct keys for repositories
        [TestMethod]
        public async Task Correct_repositories_are_called_with_tenant_and_customer_keys()
        {
            var tenant = Guid.NewGuid();
            var customerId = Guid.NewGuid();
            var orderId = 50005L;
            var order = BuildOrder(tenant, customerId, DateTimeOffset.UtcNow.AddMinutes(-30),
                grandTotal: 2000, settled: 500, status: OrderStatus.Created);
            var orderDomain = BuildInitializedOrderDomain(order);

            var orderRepo = new Mock<IDomainRepository<OrderDomain, Order>>(MockBehavior.Strict);
            _ = orderRepo
                .Setup(r => r.GetAsync(
                    Order.BuildPartitionKey(customerId),
                    Order.BuildRowKey(orderId),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(orderDomain);

            // We only need to observe the scoping keys; throw after to stop early and keep test focused
            var customerRepo = new Mock<IDomainRepository<CustomerDomain, Customer>>(MockBehavior.Strict);
            _ = customerRepo
                .Setup(r => r.GetAsync(
                    Customer.BuildPartitionKey(tenant),
                    Customer.BuildRowKey(customerId),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("StopAfterVerify"));

            var flow = new SettleFlow(orderRepo.Object, customerRepo.Object);

            // Act
            var tx = BuildTransaction(tenant, customerId, orderId, -1500);
            try
            {
                await flow.RunAsync(tx, CancellationToken.None);
            }
            catch (Exception e) when (e.Message == "StopAfterVerify")
            {
                // expected: finished verifying repository keys
            }

            // Assert (business outcome)
            orderRepo.Verify(r => r.GetAsync(
                Order.BuildPartitionKey(customerId),
                Order.BuildRowKey(orderId),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()), Times.Once);

            customerRepo.Verify(r => r.GetAsync(
                Customer.BuildPartitionKey(tenant),
                Customer.BuildRowKey(customerId),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        // Test data builders (improve readability and keep domain real)
        private static Transaction BuildTransaction(Guid tenant, Guid customerId, long orderId, long delta)
            => new()
            {
                PartitionKey = customerId.ToString(),
                Tenant = tenant.ToString(),
                Reference = orderId.ToString(),
                RowKey = Guid.NewGuid().ToString(),
                Delta = delta
            };

        private static Order BuildOrder(Guid tenant, Guid customerId, DateTimeOffset created, long grandTotal, long settled, OrderStatus status)
            => new()
            {
                Customer = customerId,
                Created = created,
                Status = (int)status,
                ShippingStatus = (int)ShippingStatus.NotApplicable,
                Tenant = tenant,
                Cart = "[]",
                Currency = "USD",
                Culture = "en-US",
                TimeZone = "UTC",
                Consignee = "John Doe",
                Email = "john@example.com",
                ShippingAddressLine1 = "123 Main St",
                ShippingCity = "City",
                ShippingCountry = "US",
                ShippingPostcode = "10001",
                BillingName = "John Doe",
                BillingAddressLine1 = "123 Main St",
                BillingCity = "City",
                BillingCountry = "US",
                BillingPostcode = "10001",
                GrandTotal = grandTotal,
                SubTotal = grandTotal,
                ShippingCost = 0,
                Tax = 0,
                Settled = settled,
            };

        private static OrderDomain BuildUninitializedOrderDomain()
        {
            // Real domain with mocked infra; remains uninitialized to simulate not-found
            var repo = new Mock<IRepository<Order>>(MockBehavior.Loose);
            var handlers = Array.Empty<IDomainEventHandler<IDomain<Order>>>();
            var options = Microsoft.Extensions.Options.Options.Create(new StoreOptions { InvoicingTenant = Guid.NewGuid() });
            var logger = new Mock<ILogger<OrderDomain>>(MockBehavior.Loose);
            return new OrderDomain(new Lazy<IRepository<Order>>(() => repo.Object), handlers, options, logger.Object);
        }

        private static OrderDomain BuildInitializedOrderDomain(Order order)
        {
            // Real domain with mocked infra; initialize with an existing order so domain logic executes
            var domain = BuildUninitializedOrderDomain();
            // Call Initialize(Order) via reflection (method may be public or non-public, on this type or a base type)
            var init = GetMethodRecursive(domain.GetType(), "Initialize", [typeof(Order)])
                ?? throw new InvalidOperationException("Could not find Initialize(Order) method via reflection.");
            _ = init.Invoke(domain, [order]);
            return domain;
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
