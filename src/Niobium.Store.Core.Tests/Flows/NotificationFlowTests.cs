using FluentAssertions;
using Moq;
using Niobium.Messaging;
using Niobium.Notification;
using Niobium.Store.Domains;
using Niobium.Store.Flows;

namespace Niobium.Store.Core.Tests.Flows
{
    // Business-focused tests for NotificationFlow mirroring the style used by other flow tests.
    // These tests keep real OrderDomain logic in scope and mock repositories + broker boundaries.
    [TestClass]
    public class NotificationFlowTests
    {
        // Scenario: Paid order triggers an order-confirmed notification including cart item details
        // Given: An order with two cart items and listings exist
        // When: NotificationFlow runs
        // Then: A single NotifyCommand is enqueued with ITEMS parameter including both listings
        [TestMethod]
        public async Task Paid_order_enqueues_notification_with_item_parameters()
        {
            // Given
            var tenant = Guid.NewGuid();
            var customer = Guid.NewGuid();
            var created = DateTimeOffset.UtcNow.AddMinutes(-5);
            var order = BuildOrder(
                tenant: tenant,
                customer: customer,
                created: created,
                status: OrderStatus.Paid,
                email: "alice@example.com",
                consignee: "Alice",
                items: new[]
                {
                    new CartItem{ Listing = 1001, Option = "default", Quantity = 2 },
                    new CartItem{ Listing = 2002, Option = "std", Quantity = 1 },
                });

            var orderDomain = BuildInitializedOrderDomain(order);

            var listing1 = BuildListing(1001, "default", name: "Widget A", price: 500, currency: "USD", sku: "SKU-1001");
            var listing2 = BuildListing(2002, "std", name: "Widget B", price: 700, currency: "USD", sku: "SKU-2002");

            var orderRepo = new Mock<IDomainRepository<OrderDomain, Order>>(MockBehavior.Strict);
            _ = orderRepo.Setup(r => r.GetAsync(It.Is<Order>(o => o == order), It.IsAny<CancellationToken>()))
                .ReturnsAsync(orderDomain);

            var listingRepo = new Mock<IRepository<Listing>>(MockBehavior.Strict);
            _ = listingRepo.Setup(r => r.RetrieveAsync(Listing.BuildPartitionKey(1001), Listing.BuildRowKey("default"), It.IsAny<IList<string>?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(listing1);
            _ = listingRepo.Setup(r => r.RetrieveAsync(Listing.BuildPartitionKey(2002), Listing.BuildRowKey("std"), It.IsAny<IList<string>?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(listing2);

            var broker = new Mock<IMessagingBroker<NotifyCommand>>();
            var flow = new NotificationFlow(broker.Object, orderRepo.Object, listingRepo.Object);

            // When
            var cmd = await flow.RunAsync(order, CancellationToken.None);

            // Then
            _ = cmd.Should().NotBeNull();
            _ = cmd.Channel.Should().Be("OrderConfirmed");
            _ = cmd.Destination.Should().Be("alice@example.com");
            _ = cmd.DestinationDisplayName.Should().Be("Alice");

            // Validate ITEMS parameter built from listings
            _ = cmd.Parameters.Should().ContainKey("ITEMS");
            var items = cmd.Parameters["ITEMS"] as IEnumerable<Dictionary<string, string>>;
            _ = items.Should().NotBeNull();
            var list = items!.ToList();
            _ = list.Should().HaveCount(2);
            _ = list[0]["NAME"].Should().Be("Widget A");
            _ = list[0]["SKU"].Should().Be("SKU-1001");
            _ = list[0]["QUANTITY"].Should().Be("2");
            _ = list[1]["NAME"].Should().Be("Widget B");
            _ = list[1]["SKU"].Should().Be("SKU-2002");
            _ = list[1]["QUANTITY"].Should().Be("1");

            orderRepo.Verify(r => r.GetAsync(order, It.IsAny<CancellationToken>()), Times.Once);
            listingRepo.Verify(r => r.RetrieveAsync(Listing.BuildPartitionKey(1001), Listing.BuildRowKey("default"), It.IsAny<IList<string>?>(), It.IsAny<CancellationToken>()), Times.Once);
            listingRepo.Verify(r => r.RetrieveAsync(Listing.BuildPartitionKey(2002), Listing.BuildRowKey("std"), It.IsAny<IList<string>?>(), It.IsAny<CancellationToken>()), Times.Once);
            broker.Verify(b => b.EnqueueAsync(It.IsAny<IEnumerable<MessagingEntry<NotifyCommand>>>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        // Scenario: Order in a status that doesn't produce a notification should not enqueue
        // Given: An order in Created status
        // When: NotificationFlow runs
        // Then: No message is enqueued and listings are not read
        [TestMethod]
        public async Task Non_notifying_status_does_not_enqueue()
        {
            var order = BuildOrder(
                tenant: Guid.NewGuid(),
                customer: Guid.NewGuid(),
                created: DateTimeOffset.UtcNow.AddHours(-1),
                status: OrderStatus.Created,
                email: "nobody@example.com",
                consignee: "Nobody",
                items: Array.Empty<CartItem>());

            var orderDomain = BuildInitializedOrderDomain(order);

            var orderRepo = new Mock<IDomainRepository<OrderDomain, Order>>(MockBehavior.Strict);
            _ = orderRepo.Setup(r => r.GetAsync(order, It.IsAny<CancellationToken>())).ReturnsAsync(orderDomain);

            var listingRepo = new Mock<IRepository<Listing>>(MockBehavior.Strict);
            var broker = new Mock<IMessagingBroker<NotifyCommand>>(MockBehavior.Strict);

            var flow = new NotificationFlow(broker.Object, orderRepo.Object, listingRepo.Object);
            await flow.RunAsync(order, CancellationToken.None);

            broker.Verify(b => b.EnqueueAsync(It.IsAny<IEnumerable<MessagingEntry<NotifyCommand>>>(), It.IsAny<CancellationToken>()), Times.Never);
            listingRepo.Verify(r => r.RetrieveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IList<string>?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // Scenario: Missing listing should be skipped without failing the notification
        // Given: An order with two items, one listing missing
        // When: NotificationFlow runs
        // Then: ITEMS contains only the found listing and a message is still enqueued
        [TestMethod]
        public async Task Missing_listing_is_skipped_and_message_still_enqueued()
        {
            var order = BuildOrder(
                tenant: Guid.NewGuid(),
                customer: Guid.NewGuid(),
                created: DateTimeOffset.UtcNow.AddMinutes(-20),
                status: OrderStatus.Paid,
                email: "bob@example.com",
                consignee: "Bob",
                items: new[]
                {
                    new CartItem{ Listing = 1, Option = "std", Quantity = 1 },
                    new CartItem{ Listing = 2, Option = "pro", Quantity = 3 },
                });

            var orderDomain = BuildInitializedOrderDomain(order);

            var orderRepo = new Mock<IDomainRepository<OrderDomain, Order>>(MockBehavior.Strict);
            _ = orderRepo.Setup(r => r.GetAsync(order, It.IsAny<CancellationToken>())).ReturnsAsync(orderDomain);

            var listingRepo = new Mock<IRepository<Listing>>(MockBehavior.Strict);
            _ = listingRepo.Setup(r => r.RetrieveAsync(Listing.BuildPartitionKey(1), Listing.BuildRowKey("std"), It.IsAny<IList<string>?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Listing?)null);
            var listing2 = BuildListing(2, "pro", name: "Item 2", price: 400, currency: "USD", sku: "SKU-2");
            _ = listingRepo.Setup(r => r.RetrieveAsync(Listing.BuildPartitionKey(2), Listing.BuildRowKey("pro"), It.IsAny<IList<string>?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(listing2);

            var broker = new Mock<IMessagingBroker<NotifyCommand>>();
            var flow = new NotificationFlow(broker.Object, orderRepo.Object, listingRepo.Object);
            var captured = await flow.RunAsync(order, CancellationToken.None);

            _ = captured.Should().NotBeNull();
            var items = captured.Parameters["ITEMS"] as IEnumerable<Dictionary<string, string>>;
            _ = items.Should().NotBeNull();
            var list = items!.ToList();
            _ = list.Should().HaveCount(1);
            _ = list[0]["NAME"].Should().Be("Item 2");
            _ = list[0]["QUANTITY"].Should().Be("3");

            broker.Verify(b => b.EnqueueAsync(It.IsAny<IEnumerable<MessagingEntry<NotifyCommand>>>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        // Scenario: Empty cart still sends a header-only notification (ITEMS empty)
        // Given: A paid order with no cart items
        // When: NotificationFlow runs
        // Then: A message is enqueued and ITEMS is an empty list
        [TestMethod]
        public async Task Empty_cart_enqueues_notification_with_empty_items()
        {
            var order = BuildOrder(
                tenant: Guid.NewGuid(),
                customer: Guid.NewGuid(),
                created: DateTimeOffset.UtcNow.AddMinutes(-45),
                status: OrderStatus.Paid,
                email: "eve@example.com",
                consignee: "Eve",
                items: Array.Empty<CartItem>());

            var orderDomain = BuildInitializedOrderDomain(order);

            var orderRepo = new Mock<IDomainRepository<OrderDomain, Order>>(MockBehavior.Strict);
            _ = orderRepo.Setup(r => r.GetAsync(order, It.IsAny<CancellationToken>())).ReturnsAsync(orderDomain);

            var listingRepo = new Mock<IRepository<Listing>>(MockBehavior.Strict);

            var broker = new Mock<IMessagingBroker<NotifyCommand>>();
            var flow = new NotificationFlow(broker.Object, orderRepo.Object, listingRepo.Object);
            var captured = await flow.RunAsync(order, CancellationToken.None);

            _ = captured.Should().NotBeNull();
            var items = captured.Parameters["ITEMS"] as IEnumerable<Dictionary<string, string>>;
            _ = items.Should().NotBeNull();
            _ = items!.Should().BeEmpty();
        }

        // --- Builders and helpers ---
        private static Order BuildOrder(
            Guid tenant,
            Guid customer,
            DateTimeOffset created,
            OrderStatus status,
            string email,
            string consignee,
            IEnumerable<CartItem> items)
        {
            var order = new Order
            {
                Customer = customer,
                Created = created,
                Status = (int)status,
                ShippingStatus = (int)ShippingStatus.NotApplicable,
                Tenant = tenant,
                Currency = "USD",
                Culture = "en-US",
                TimeZone = "UTC",
                Consignee = consignee,
                Email = email,
                ShippingAddressLine1 = "123 Main St",
                ShippingCity = "City",
                ShippingCountry = "US",
                ShippingPostcode = "10001",
                BillingName = consignee,
                BillingAddressLine1 = "123 Main St",
                BillingCity = "City",
                BillingCountry = "US",
                BillingPostcode = "10001",
                Discount = 0,
                ShippingCost = 0,
                Tax = 0,
                Settled = 0,
                Cart = "[]",
            };
            order.SetCart(items);
            return order;
        }

        private static Listing BuildListing(int id, string option, string name, long price, string currency, string sku)
            => new()
            {
                ID = id,
                Option = option,
                Name = name,
                Price = price,
                Currency = currency,
                SKU = sku,
                TaxRate = 0,
                TaxKind = 0,
                ShippingOptions = "10",
                Culture = "en-US",
            };

        private static OrderDomain BuildInitializedOrderDomain(Order order)
        {
            var repo = new Mock<IRepository<Order>>(MockBehavior.Loose);
            var handlers = Array.Empty<IDomainEventHandler<IDomain<Order>>>();
            var options = Microsoft.Extensions.Options.Options.Create(new Niobium.Store.Options.StoreOptions { InvoicingTenant = Guid.NewGuid() });
            var logger = new Mock<Microsoft.Extensions.Logging.ILogger<OrderDomain>>(MockBehavior.Loose);
            var domain = new OrderDomain(new Lazy<IRepository<Order>>(() => repo.Object), handlers, options, logger.Object);
            InitializeDomain(domain, order);
            return domain;
        }

        private static void InitializeDomain<TDomain, TEntity>(TDomain domain, TEntity entity)
        {
            var method = GetMethodRecursive(domain!.GetType(), "Initialize", new[] { typeof(TEntity) })
                ?? throw new InvalidOperationException($"Could not find Initialize({typeof(TEntity).Name}) on {domain.GetType().Name}");
            _ = method.Invoke(domain, new object[] { entity! });
        }

        private static System.Reflection.MethodInfo? GetMethodRecursive(Type type, string name, Type[] parameterTypes)
        {
            while (type != null)
            {
                var mi = type.GetMethod(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, binder: null, types: parameterTypes, modifiers: null);
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
