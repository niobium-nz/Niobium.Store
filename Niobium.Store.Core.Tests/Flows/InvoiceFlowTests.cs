using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Niobium.Invoicing;
using Niobium.Messaging;
using Niobium.Store.Domains;
using Niobium.Store.Flows;
using Niobium.Store.Options;

namespace Niobium.Store.Core.Tests.Flows
{
    // Purpose
    // Business-focused tests for InvoiceFlow that keep real Domain logic in scope (OrderDomain, ListingDomain)
    // and mock only repositories and the messaging broker. The tests are scenario-driven and junior-friendly.
    [TestClass]
    public class InvoiceFlowTests
    {
        // Scenario: Issuing an invoice for a typical order assembles correct invoice items and enqueues a single message
        // Given: An order with two cart lines; each listing exists with price/currency and a name
        // When: The invoice is issued for the order
        // Then: A single invoice message is enqueued with accurate header and line details for the shopper and accounting
        [TestMethod]
        public async Task Issuing_invoice_for_order_enqueues_message_with_correct_items()
        {
            // Given
            var tenant = Guid.NewGuid();
            var customer = Guid.NewGuid();
            var created = DateTimeOffset.UtcNow.AddMinutes(-5);
            var order = BuildOrder(
                tenant: tenant,
                customer: customer,
                created: created,
                currency: "USD",
                settled: 0,
                billingName: "Alice",
                email: "alice@example.com",
                items:
                [
                    new CartItem{ Listing = 1001, Option = "default", Quantity = 2 },
                    new CartItem{ Listing = 2002, Option = "std", Quantity = 1 },
                ]);

            var invoiceTenant = Guid.NewGuid();
            var orderDomain = BuildInitializedOrderDomain(order, invoiceTenant);

            var listing1 = BuildListing(1001, "default", name: "Widget A", price: 500, currency: "USD", taxRate: 0, taxKind: 0);
            var listing2 = BuildListing(2002, "std", name: "Widget B", price: 700, currency: "USD", taxRate: 0, taxKind: 0);
            var listingDomain1 = BuildInitializedListingDomain(listing1);
            var listingDomain2 = BuildInitializedListingDomain(listing2);

            // Repositories (infrastructure boundaries)
            var orderRepo = new Mock<IDomainRepository<OrderDomain, Order>>(MockBehavior.Strict);
            _ = orderRepo.Setup(r => r.GetAsync(It.Is<Order>(o => o == order), It.IsAny<CancellationToken>()))
                .ReturnsAsync(orderDomain);

            var listingRepo = new Mock<IDomainRepository<ListingDomain, Listing>>(MockBehavior.Strict);
            _ = listingRepo.Setup(r => r.GetAsync(Listing.BuildPartitionKey(1001), Listing.BuildRowKey("default"), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(listingDomain1);
            _ = listingRepo.Setup(r => r.GetAsync(Listing.BuildPartitionKey(2002), Listing.BuildRowKey("std"), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(listingDomain2);

            var promotionRepo = new Mock<IDomainRepository<PromotionDomain, Promotion>>(MockBehavior.Strict);

            // Broker to capture the outgoing message
            var broker = new Mock<IMessagingBroker<IssueInvoiceCommand>>(MockBehavior.Strict);
            MessagingEntry<IssueInvoiceCommand>? captured = null;
            _ = broker.Setup(b => b.EnqueueAsync(It.IsAny<IEnumerable<MessagingEntry<IssueInvoiceCommand>>>(), It.IsAny<CancellationToken>()))
                .Callback<IEnumerable<MessagingEntry<IssueInvoiceCommand>>, CancellationToken>((m, _) => captured = m.Single())
                .Returns(Task.CompletedTask);

            var flow = new InvoiceFlow(broker.Object, orderRepo.Object, promotionRepo.Object, listingRepo.Object);

            // When
            await flow.RunAsync(order, CancellationToken.None);

            // Then
            _ = captured.Should().NotBeNull();
            _ = captured!.ID.Should().Be(order.GetFullID());
            var invoice = captured!.Value;
            _ = invoice.InvoiceID.Should().Be(order.GetID());
            _ = invoice.BilleeID.Should().Be(order.Customer);
            _ = invoice.BillerID.Should().Be(order.Tenant);
            _ = invoice.Tenant.Should().Be(invoiceTenant); // comes from OrderDomain options
            _ = invoice.Settled.Cents.Should().Be(order.Settled);
            _ = invoice.Settled.Currency.ToString().Should().Be(order.Currency);
            _ = invoice.Billee.Name.Should().Be("Alice");
            _ = invoice.Billee.Email.Should().Be("alice@example.com");

            // Items
            _ = invoice.InvoiceItems.Should().HaveCount(2);
            var line1 = invoice.InvoiceItems[0];
            var line2 = invoice.InvoiceItems[1];
            _ = line1.Description.Should().Be("Widget A");
            _ = line1.Quantity.Should().Be(2);
            _ = line1.UnitPriceCents.Should().Be(500);
            _ = line1.LineTotalCents.Should().Be(1000);
            _ = line1.UnitPriceCurrency.Should().Be("USD");
            _ = line1.LineTotalCurrency.Should().Be("USD");
            _ = line1.ID.Should().Be(invoice.InvoiceID + 1001);

            _ = line2.Description.Should().Be("Widget B");
            _ = line2.Quantity.Should().Be(1);
            _ = line2.UnitPriceCents.Should().Be(700);
            _ = line2.LineTotalCents.Should().Be(700);
            _ = line2.UnitPriceCurrency.Should().Be("USD");
            _ = line2.LineTotalCurrency.Should().Be("USD");
            _ = line2.ID.Should().Be(invoice.InvoiceID + 2002);

            // Repository scoping verification
            orderRepo.Verify(r => r.GetAsync(order, It.IsAny<CancellationToken>()), Times.Once);
            listingRepo.Verify(r => r.GetAsync(Listing.BuildPartitionKey(1001), Listing.BuildRowKey("default"), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
            listingRepo.Verify(r => r.GetAsync(Listing.BuildPartitionKey(2002), Listing.BuildRowKey("std"), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
            broker.Verify(b => b.EnqueueAsync(It.IsAny<IEnumerable<MessagingEntry<IssueInvoiceCommand>>>(), It.IsAny<CancellationToken>()), Times.Once);
            promotionRepo.VerifyNoOtherCalls();
        }

        // Scenario: A cart item must have a positive quantity
        // Given: An order whose cart has a line with Quantity == 0
        // When: The flow builds the invoice
        // Then: The business rejects the line and no message is enqueued
        [TestMethod]
        public async Task Zero_quantity_item_causes_failure_and_no_message()
        {
            var tenant = Guid.NewGuid();
            var customer = Guid.NewGuid();
            var created = DateTimeOffset.UtcNow.AddMinutes(-10);
            var order = BuildOrder(
                tenant, customer, created, currency: "USD", settled: 0,
                items: [new CartItem { Listing = 42, Option = "Default", Quantity = 0 }],
                billingName: "Bob", email: "bob@example.com");

            var orderDomain = BuildInitializedOrderDomain(order, Guid.NewGuid());

            var listing = BuildListing(42, "Default", name: "Thing", price: 300, currency: "USD", taxRate: 0, taxKind: 0);
            var listingDomain = BuildInitializedListingDomain(listing);

            var orderRepo = new Mock<IDomainRepository<OrderDomain, Order>>(MockBehavior.Strict);
            _ = orderRepo.Setup(r => r.GetAsync(order, It.IsAny<CancellationToken>())).ReturnsAsync(orderDomain);

            var listingRepo = new Mock<IDomainRepository<ListingDomain, Listing>>(MockBehavior.Strict);
            _ = listingRepo.Setup(r => r.GetAsync(Listing.BuildPartitionKey(42), Listing.BuildRowKey("Default"), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(listingDomain);

            var promotionRepo = new Mock<IDomainRepository<PromotionDomain, Promotion>>(MockBehavior.Strict);

            var broker = new Mock<IMessagingBroker<IssueInvoiceCommand>>(MockBehavior.Strict);

            var flow = new InvoiceFlow(broker.Object, orderRepo.Object, promotionRepo.Object, listingRepo.Object);

            Func<Task> act = async () => await flow.RunAsync(order, CancellationToken.None);
            _ = await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
            broker.Verify(b => b.EnqueueAsync(It.IsAny<IEnumerable<MessagingEntry<IssueInvoiceCommand>>>(), It.IsAny<CancellationToken>()), Times.Never);
            promotionRepo.VerifyNoOtherCalls();
        }

        // Scenario: Missing listing prevents invoicing
        // Given: An order with a cart item but the listing is missing
        // When: The flow runs
        // Then: The flow fails and does not enqueue a message
        [TestMethod]
        public async Task Missing_listing_causes_failure_and_no_message()
        {
            var tenant = Guid.NewGuid();
            var customer = Guid.NewGuid();
            var created = DateTimeOffset.UtcNow.AddHours(-1);
            var order = BuildOrder(
                tenant, customer, created, currency: "USD", settled: 0,
                items: [new CartItem { Listing = 7, Option = "x", Quantity = 1 }],
                billingName: "Carol", email: "carol@example.com");

            var orderDomain = BuildInitializedOrderDomain(order, Guid.NewGuid());

            // Uninitialized domain to simulate not found listing
            var uninitializedListingDomain = BuildUninitializedListingDomain();

            var orderRepo = new Mock<IDomainRepository<OrderDomain, Order>>(MockBehavior.Strict);
            _ = orderRepo.Setup(r => r.GetAsync(order, It.IsAny<CancellationToken>())).ReturnsAsync(orderDomain);

            var listingRepo = new Mock<IDomainRepository<ListingDomain, Listing>>(MockBehavior.Strict);
            _ = listingRepo.Setup(r => r.GetAsync(Listing.BuildPartitionKey(7), Listing.BuildRowKey("x"), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(uninitializedListingDomain);

            var promotionRepo = new Mock<IDomainRepository<PromotionDomain, Promotion>>(MockBehavior.Strict);

            var broker = new Mock<IMessagingBroker<IssueInvoiceCommand>>(MockBehavior.Strict);

            var flow = new InvoiceFlow(broker.Object, orderRepo.Object, promotionRepo.Object, listingRepo.Object);

            Func<Task> act = async () => await flow.RunAsync(order, CancellationToken.None);
            _ = await act.Should().ThrowAsync<ApplicationException>().Where(e => e.ErrorCode == InternalError.NotFound);
            broker.Verify(b => b.EnqueueAsync(It.IsAny<IEnumerable<MessagingEntry<IssueInvoiceCommand>>>(), It.IsAny<CancellationToken>()), Times.Never);
            promotionRepo.VerifyNoOtherCalls();
        }

        // Scenario: Header accuracy for a partially settled order
        // Given: An order that is partially settled and has full billing/contact details
        // When: The invoice is issued
        // Then: The invoice header carries the correct settled amount, currency, and billee details
        [TestMethod]
        public async Task Partially_settled_order_has_correct_header_fields()
        {
            var tenant = Guid.NewGuid();
            var customer = Guid.NewGuid();
            var created = DateTimeOffset.UtcNow.AddDays(-1);
            var order = BuildOrder(
                tenant, customer, created, currency: "USD", settled: 1200,
                items: [new CartItem { Listing = 5, Option = "std", Quantity = 3 }],
                billingName: "Dora", email: "dora@example.com",
                billing: ("10 Queen St", "", "Auckland", null, "NZ", "1010"));

            var invoiceTenant = Guid.NewGuid();
            var orderDomain = BuildInitializedOrderDomain(order, invoiceTenant);

            var listing = BuildListing(5, "std", name: "Brush", price: 400, currency: "USD", taxRate: 0, taxKind: 0);
            var listingDomain = BuildInitializedListingDomain(listing);

            var orderRepo = new Mock<IDomainRepository<OrderDomain, Order>>(MockBehavior.Strict);
            _ = orderRepo.Setup(r => r.GetAsync(order, It.IsAny<CancellationToken>())).ReturnsAsync(orderDomain);

            var listingRepo = new Mock<IDomainRepository<ListingDomain, Listing>>(MockBehavior.Strict);
            _ = listingRepo.Setup(r => r.GetAsync(Listing.BuildPartitionKey(5), Listing.BuildRowKey("std"), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(listingDomain);

            var promotionRepo = new Mock<IDomainRepository<PromotionDomain, Promotion>>(MockBehavior.Strict);

            var broker = new Mock<IMessagingBroker<IssueInvoiceCommand>>(MockBehavior.Strict);
            MessagingEntry<IssueInvoiceCommand>? captured = null;
            _ = broker.Setup(b => b.EnqueueAsync(It.IsAny<IEnumerable<MessagingEntry<IssueInvoiceCommand>>>(), It.IsAny<CancellationToken>()))
                .Callback<IEnumerable<MessagingEntry<IssueInvoiceCommand>>, CancellationToken>((m, _) => captured = m.Single())
                .Returns(Task.CompletedTask);

            var flow = new InvoiceFlow(broker.Object, orderRepo.Object, promotionRepo.Object, listingRepo.Object);
            await flow.RunAsync(order, CancellationToken.None);

            _ = captured.Should().NotBeNull();
            var invoice = captured!.Value;
            _ = invoice.Tenant.Should().Be(invoiceTenant);
            _ = invoice.Settled.Cents.Should().Be(1200);
            _ = invoice.Settled.Currency.ToString().Should().Be("USD");
            _ = invoice.Billee.Name.Should().Be("Dora");
            _ = invoice.Billee.Email.Should().Be("dora@example.com");
            _ = invoice.Billee.AddressLine1.Should().Be("10 Queen St");
            _ = invoice.Billee.City.Should().Be("Auckland");
            _ = invoice.Billee.State.Should().BeNull();
            _ = invoice.Billee.Country.Should().Be("NZ");
            _ = invoice.Billee.Zipcode.Should().Be("1010");

            promotionRepo.VerifyNoOtherCalls();
        }

        // Scenario: Empty cart produces a header-only invoice (current behavior)
        // Given: An order with no cart items
        // When: The invoice is issued
        // Then: A single message is enqueued with zero invoice items
        [TestMethod]
        public async Task Empty_cart_enqueues_header_only_invoice()
        {
            var tenant = Guid.NewGuid();
            var customer = Guid.NewGuid();
            var created = DateTimeOffset.UtcNow.AddMinutes(-30);
            var order = BuildOrder(
                tenant, customer, created, currency: "USD", settled: 0,
                items: Array.Empty<CartItem>(),
                billingName: "Eve", email: "eve@example.com");

            var orderDomain = BuildInitializedOrderDomain(order, Guid.NewGuid());

            var orderRepo = new Mock<IDomainRepository<OrderDomain, Order>>(MockBehavior.Strict);
            _ = orderRepo.Setup(r => r.GetAsync(order, It.IsAny<CancellationToken>())).ReturnsAsync(orderDomain);

            var listingRepo = new Mock<IDomainRepository<ListingDomain, Listing>>(MockBehavior.Strict);
            var promotionRepo = new Mock<IDomainRepository<PromotionDomain, Promotion>>(MockBehavior.Strict);
            var broker = new Mock<IMessagingBroker<IssueInvoiceCommand>>(MockBehavior.Strict);
            MessagingEntry<IssueInvoiceCommand>? captured = null;
            _ = broker.Setup(b => b.EnqueueAsync(It.IsAny<IEnumerable<MessagingEntry<IssueInvoiceCommand>>>(), It.IsAny<CancellationToken>()))
                .Callback<IEnumerable<MessagingEntry<IssueInvoiceCommand>>, CancellationToken>((m, _) => captured = m.Single())
                .Returns(Task.CompletedTask);

            var flow = new InvoiceFlow(broker.Object, orderRepo.Object, promotionRepo.Object, listingRepo.Object);
            await flow.RunAsync(order, CancellationToken.None);

            _ = captured.Should().NotBeNull();
            _ = captured!.Value.InvoiceItems.Should().BeEmpty();
            promotionRepo.VerifyNoOtherCalls();
        }

        // Scenario: Sequencing of invoice item IDs is deterministic and follows cart order
        // Given: An order with three items in a defined order
        // When: The invoice is issued
        // Then: Invoice item IDs are invoiceID + 0/1/2 in the same order
        [TestMethod]
        public async Task Invoice_item_ids_follow_cart_sequence()
        {
            var tenant = Guid.NewGuid();
            var customer = Guid.NewGuid();
            var created = DateTimeOffset.UtcNow.AddMinutes(-15);
            var order = BuildOrder(
                tenant, customer, created, currency: "USD", settled: 0,
                items:
                [
                    new CartItem{ Listing = 1, Option = "a", Quantity = 1 },
                    new CartItem{ Listing = 2, Option = "b", Quantity = 1 },
                    new CartItem{ Listing = 3, Option = "c", Quantity = 1 },
                ],
                billingName: "Fred", email: "fred@example.com");

            var orderDomain = BuildInitializedOrderDomain(order, Guid.NewGuid());

            var listingRepo = new Mock<IDomainRepository<ListingDomain, Listing>>(MockBehavior.Strict);
            foreach (var i in new[] { 1, 2, 3 })
            {
                var l = BuildListing(i, ((char)('a' + (i - 1))).ToString(), name: $"Item {i}", price: 100 * i, currency: "USD", taxRate: 0, taxKind: 0);
                var d = BuildInitializedListingDomain(l);
                _ = listingRepo.Setup(r => r.GetAsync(Listing.BuildPartitionKey(i), Listing.BuildRowKey(l.Option), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(d);
            }

            var orderRepo = new Mock<IDomainRepository<OrderDomain, Order>>(MockBehavior.Strict);
            _ = orderRepo.Setup(r => r.GetAsync(order, It.IsAny<CancellationToken>())).ReturnsAsync(orderDomain);

            var promotionRepo = new Mock<IDomainRepository<PromotionDomain, Promotion>>(MockBehavior.Strict);

            var broker = new Mock<IMessagingBroker<IssueInvoiceCommand>>(MockBehavior.Strict);
            MessagingEntry<IssueInvoiceCommand>? captured = null;
            _ = broker.Setup(b => b.EnqueueAsync(It.IsAny<IEnumerable<MessagingEntry<IssueInvoiceCommand>>>(), It.IsAny<CancellationToken>()))
                .Callback<IEnumerable<MessagingEntry<IssueInvoiceCommand>>, CancellationToken>((m, _) => captured = m.Single())
                .Returns(Task.CompletedTask);

            var flow = new InvoiceFlow(broker.Object, orderRepo.Object, promotionRepo.Object, listingRepo.Object);
            await flow.RunAsync(order, CancellationToken.None);

            _ = captured.Should().NotBeNull();
            var invoice = captured!.Value;
            _ = invoice.InvoiceItems.Should().HaveCount(3);
            _ = invoice.InvoiceItems[0].ID.Should().Be(invoice.InvoiceID + 1);
            _ = invoice.InvoiceItems[1].ID.Should().Be(invoice.InvoiceID + 2);
            _ = invoice.InvoiceItems[2].ID.Should().Be(invoice.InvoiceID + 3);
            promotionRepo.VerifyNoOtherCalls();
        }

        // --- Builders and helpers (keep tests readable and domains real) ---
        private static Order BuildOrder(
            Guid tenant,
            Guid customer,
            DateTimeOffset created,
            string currency,
            long settled,
            IEnumerable<CartItem> items,
            string billingName,
            string email,
            (string line1, string? line2, string city, string? state, string country, string postcode)? billing = null)
        {
            var order = new Order
            {
                Customer = customer,
                Created = created,
                Status = (int)OrderStatus.Created,
                ShippingStatus = (int)ShippingStatus.NotApplicable,
                Tenant = tenant,
                Currency = currency,
                Culture = "en-US",
                TimeZone = "UTC",
                Consignee = billingName,
                Email = email,
                ShippingAddressLine1 = "123 Main St",
                ShippingCity = "City",
                ShippingCountry = "US",
                ShippingPostcode = "10001",
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
                Settled = settled,
                Cart = "[]",
            };
            order.SetCart(items);
            return order;
        }

        private static Listing BuildListing(int id, string option, string name, long price, string currency, long taxRate, int taxKind)
            => new()
            {
                ID = id,
                Option = option,
                Name = name,
                Price = price,
                Currency = currency,
                SKU = $"SKU-{id}",
                TaxRate = taxRate,
                TaxKind = taxKind,
                ShippingOptions = "10",
                Culture = "en-US",
            };

        private static OrderDomain BuildInitializedOrderDomain(Order order, Guid invoicingTenant)
        {
            var repo = new Mock<IRepository<Order>>(MockBehavior.Loose);
            var handlers = Array.Empty<IDomainEventHandler<IDomain<Order>>>();
            var options = Microsoft.Extensions.Options.Options.Create(new StoreOptions { InvoicingTenant = invoicingTenant });
            var logger = new Mock<ILogger<OrderDomain>>(MockBehavior.Loose);
            var domain = new OrderDomain(new Lazy<IRepository<Order>>(() => repo.Object), handlers, options, logger.Object);
            InitializeDomain(domain, order);
            return domain;
        }

        private static ListingDomain BuildInitializedListingDomain(Listing listing)
        {
            var repo = new Mock<IRepository<Listing>>(MockBehavior.Loose);
            var handlers = Array.Empty<IDomainEventHandler<IDomain<Listing>>>();
            var domain = new ListingDomain(new Lazy<IRepository<Listing>>(() => repo.Object), handlers);
            InitializeDomain(domain, listing);
            return domain;
        }

        private static ListingDomain BuildUninitializedListingDomain()
        {
            var repo = new Mock<IRepository<Listing>>(MockBehavior.Loose);
            var handlers = Array.Empty<IDomainEventHandler<IDomain<Listing>>>();
            return new ListingDomain(new Lazy<IRepository<Listing>>(() => repo.Object), handlers);
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
