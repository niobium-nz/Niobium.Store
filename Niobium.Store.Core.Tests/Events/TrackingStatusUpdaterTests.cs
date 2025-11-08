using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Niobium.Store.Domains;
using Niobium.Store.Events;
using Niobium.Store.Options;

namespace Niobium.Store.Core.Tests.Events
{
    // Purpose
    // These tests document the simple behavior of TrackingStatusUpdater:
    // - It moves an order's Status forward when asked (e.g. Paid -> Shipped).
    // - It never moves the Status backwards (e.g. Shipped -> Paid is ignored).
    // - It can advance through later stages (e.g. Shipped -> Delivered).
    // The tests avoid domain jargon; think of "Status" as a progress marker.
    // We use the real OrderDomain so persistence/update logic is exercised.
    // Only the repository boundary is mocked.
    [TestClass]
    public class TrackingStatusUpdaterTests
    {
        // Scenario: Advancing the order to the next stage
        // Given an order currently marked as Paid (progress stage20)
        // When a tracking update requests Shipped (stage30)
        // Then the stored order's status becomes Shipped
        [TestMethod]
        public async Task Tracking_update_advances_status_forward()
        {
            // Given
            var tenantId = Guid.NewGuid();
            var customerId = Guid.NewGuid();
            var order = BuildOrder(tenantId, customerId, DateTimeOffset.UtcNow.AddMinutes(-30), initialStatus: OrderStatus.Paid);
            var orderDomain = BuildInitializedOrderDomain(order);

            var repo = new Mock<IDomainRepository<OrderDomain, Order>>(MockBehavior.Strict);
            _ = repo.Setup(r => r.GetAsync(Order.BuildPartitionKey(customerId), Order.BuildRowKey(order.GetID()), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(orderDomain);

            var handler = new TrackingStatusUpdater(repo.Object);
            var command = new UpdateTrackingCommand
            {
                Customer = customerId,
                Order = order.GetID(),
                Status = OrderStatus.Shipped
            };

            // When
            await handler.HandleAsync(command, CancellationToken.None);

            // Then
            _ = order.Status.Should().Be((int)OrderStatus.Shipped, "status should move forward to requested stage");
            repo.Verify(r => r.GetAsync(Order.BuildPartitionKey(customerId), Order.BuildRowKey(order.GetID()), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        // Scenario: Requesting a backward move is ignored
        // Given an order already at Shipped (stage30)
        // When a tracking update requests a lower stage Paid (stage20)
        // Then the order stays at Shipped (no regression)
        [TestMethod]
        public async Task Tracking_update_behind_current_status_is_ignored()
        {
            // Given
            var tenantId = Guid.NewGuid();
            var customerId = Guid.NewGuid();
            var order = BuildOrder(tenantId, customerId, DateTimeOffset.UtcNow.AddHours(-1), initialStatus: OrderStatus.Shipped);
            var orderDomain = BuildInitializedOrderDomain(order);

            var repo = new Mock<IDomainRepository<OrderDomain, Order>>(MockBehavior.Strict);
            _ = repo.Setup(r => r.GetAsync(Order.BuildPartitionKey(customerId), Order.BuildRowKey(order.GetID()), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(orderDomain);

            var handler = new TrackingStatusUpdater(repo.Object);
            var command = new UpdateTrackingCommand
            {
                Customer = customerId,
                Order = order.GetID(),
                Status = OrderStatus.Paid // backward request
            };

            // When
            await handler.HandleAsync(command, CancellationToken.None);

            // Then
            _ = order.Status.Should().Be((int)OrderStatus.Shipped, "status must not move backwards");
            repo.Verify(r => r.GetAsync(Order.BuildPartitionKey(customerId), Order.BuildRowKey(order.GetID()), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        // Scenario: Advancing from an intermediate stage to a later stage
        // Given an order at Shipped (stage30)
        // When a tracking update requests Delivered (stage40)
        // Then the order reflects Delivered
        [TestMethod]
        public async Task Tracking_update_to_delivered_advances_from_shipped()
        {
            // Given
            var tenantId = Guid.NewGuid();
            var customerId = Guid.NewGuid();
            var order = BuildOrder(tenantId, customerId, DateTimeOffset.UtcNow.AddMinutes(-10), initialStatus: OrderStatus.Shipped);
            var orderDomain = BuildInitializedOrderDomain(order);

            var repo = new Mock<IDomainRepository<OrderDomain, Order>>(MockBehavior.Strict);
            _ = repo.Setup(r => r.GetAsync(Order.BuildPartitionKey(customerId), Order.BuildRowKey(order.GetID()), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(orderDomain);

            var handler = new TrackingStatusUpdater(repo.Object);
            var command = new UpdateTrackingCommand
            {
                Customer = customerId,
                Order = order.GetID(),
                Status = OrderStatus.Delivered
            };

            // When
            await handler.HandleAsync(command, CancellationToken.None);

            // Then
            _ = order.Status.Should().Be((int)OrderStatus.Delivered, "status should advance to Delivered");
            repo.Verify(r => r.GetAsync(Order.BuildPartitionKey(customerId), Order.BuildRowKey(order.GetID()), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        // --- Helper Builders (keep object construction noise out of tests) ---
        private static Order BuildOrder(Guid tenant, Guid customer, DateTimeOffset created, OrderStatus initialStatus) => new()
        {
            Customer = customer,
            Created = created,
            Status = (int)initialStatus,
            ShippingStatus = (int)ShippingStatus.NotApplicable,
            Tenant = tenant,
            Cart = "[]",
            Currency = "USD",
            Culture = "en-US",
            TimeZone = "UTC",
            Consignee = "Jane Doe",
            Email = "jane@example.com",
            ShippingAddressLine1 = "123 Main St",
            ShippingCity = "City",
            ShippingCountry = "US",
            ShippingPostcode = "10001",
            BillingName = "Jane Doe",
            BillingAddressLine1 = "123 Main St",
            BillingCity = "City",
            BillingCountry = "US",
            BillingPostcode = "10001",
            ShippingCost = 0,
            Tax = 0,
        };

        private static OrderDomain BuildInitializedOrderDomain(Order order)
        {
            // Real domain so that UpdateTrackingAsync executes genuine logic.
            var repo = new Mock<IRepository<Order>>(MockBehavior.Loose);
            var handlers = Array.Empty<IDomainEventHandler<IDomain<Order>>>();
            var options = Microsoft.Extensions.Options.Options.Create(new StoreOptions { InvoicingTenant = Guid.NewGuid() });
            var logger = new Mock<ILogger<OrderDomain>>(MockBehavior.Loose);
            var domain = new OrderDomain(new Lazy<IRepository<Order>>(() => repo.Object), handlers, options, logger.Object);
            InitializeDomain(domain, order);
            return domain;
        }

        private static void InitializeDomain<TDomain, TEntity>(TDomain domain, TEntity entity)
        {
            // Use reflection so tests stay resilient if Initialize is non-public.
            var method = GetMethodRecursive(domain!.GetType(), "Initialize", new[] { typeof(TEntity) })
            ?? throw new InvalidOperationException($"Could not find Initialize({typeof(TEntity).Name}) on {domain.GetType().Name}");
            _ = method.Invoke(domain, [entity!]);
        }

        private static MethodInfo? GetMethodRecursive(Type type, string name, Type[] parameterTypes)
        {
            while (type != null)
            {
                var mi = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, binder: null, types: parameterTypes, modifiers: null);
                if (mi != null) return mi;
                type = type.BaseType!;
            }
            return null;
        }
    }
}