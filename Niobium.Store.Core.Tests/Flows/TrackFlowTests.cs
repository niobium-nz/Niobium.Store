using FluentAssertions;
using Moq;
using Niobium.Store.Flows;

namespace Niobium.Store.Core.Tests.Flows
{
    // Purpose
    // These tests describe TrackFlow from a customer’s perspective: a shopper enters their email and order number
    // to see their order details. The tests mock storage repositories while exercising the real flow logic.
    //
    // Key business rules covered:
    // - A customer can only track orders they own (email + order must match an Ownership record).
    // - If Ownership exists but the Order record is missing (drift), the customer sees a not-found error.
    // - Email input is normalized (trim + lowercase) so users aren’t blocked by accidental formatting.
    // - Orders are read using the tenant recorded on the Ownership, ensuring correct multi-tenant scoping.
    // - The returned details reflect the current order state and cart exactly as stored.
    [TestClass]
    public class TrackFlowTests
    {
        // Scenario: Customer tracks an order they own and sees accurate details
        // Given: Ownership exists for the (email, orderId) pair, and Order holds shipping + cart info
        // When: The user submits a valid tracking request (email + orderId)
        // Then: The flow returns order details so the customer can check status and what they purchased
        [TestMethod]
        public async Task Tracking_an_owned_order_returns_order_details()
        {
            var email = "alice@example.com";
            var tenant = Guid.NewGuid();
            var orderId = 10101L;
            var ownership = BuildOwnership(email, orderId, tenant);

            var created = DateTimeOffset.UtcNow.AddDays(-1);
            var order = BuildOrder(
                tenant: tenant,
                customerId: Guid.NewGuid(),
                created: created,
                status: OrderStatus.Created,
                shippingStatus: ShippingStatus.Pending,
                city: "Auckland",
                state: null,
                country: "NZ",
                cart: new[]
                {
                    new CartItem{ Listing = 2001, Option = "red", Quantity = 1 },
                    new CartItem{ Listing = 2002, Option = "blue", Quantity = 2 }
                });

            var ownershipRepo = new Mock<IRepository<Ownership>>(MockBehavior.Strict);
            _ = ownershipRepo
                .Setup(r => r.RetrieveAsync(
                    Ownership.BuildPartitionKey(ownership.Email),
                    Ownership.BuildRowKey(ownership.Order),
                    It.IsAny<IList<string>?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(ownership);

            var orderRepo = new Mock<IRepository<Order>>(MockBehavior.Strict);
            _ = orderRepo
                .Setup(r => r.RetrieveAsync(
                    Order.BuildPartitionKey(order.Tenant),
                    Ownership.BuildRowKey(orderId),
                    It.IsAny<IList<string>?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(order);

            var flow = new TrackFlow(ownershipRepo.Object, orderRepo.Object);

            // When the customer submits a valid tracking request
            var request = BuildTrackRequest(email, orderId);
            var response = await flow.RunAsync(request, CancellationToken.None);

            // Then the customer sees their order details
            _ = response.Created.Should().Be(created);
            _ = response.Status.Should().Be(OrderStatus.Created);
            _ = response.ShippingStatus.Should().Be(ShippingStatus.Pending);
            _ = response.ShippingCity.Should().Be("Auckland");
            _ = response.ShippingState.Should().BeNull();
            _ = response.ShippingCountry.Should().NotBeNull();
            _ = response.Cart.Should().HaveCount(2);
            _ = response.Cart.First().Listing.Should().Be(2001);
            _ = response.Cart.Last().Quantity.Should().Be(2);
        }

        // Scenario: Customer attempts to track an order they do not own
        // Given: Ownership lookup for (email, orderId) returns no record
        // When: The user tries to track with that email + orderId
        // Then: The flow rejects the request with a NotFound error to avoid leaking order info
        [TestMethod]
        public async Task Tracking_fails_when_no_ownership_exists()
        {
            var email = "bob@example.com";
            var orderId = 20202L;

            var ownershipRepo = new Mock<IRepository<Ownership>>(MockBehavior.Strict);
            _ = ownershipRepo
                .Setup(r => r.RetrieveAsync(
                    Ownership.BuildPartitionKey(email),
                    Ownership.BuildRowKey(orderId),
                    It.IsAny<IList<string>?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((Ownership?)null);

            var orderRepo = new Mock<IRepository<Order>>(MockBehavior.Strict);

            var flow = new TrackFlow(ownershipRepo.Object, orderRepo.Object);

            // When the customer tries to track
            Func<Task> act = async () => await flow.RunAsync(BuildTrackRequest(email, orderId), CancellationToken.None);

            // Then the business flow rejects with not-found error
            var ex = await act.Should().ThrowAsync<ApplicationException>();
            _ = ex.Which.ErrorCode.Should().Be(InternalError.NotFound);
        }

        // Scenario: Ownership exists but the Order record is missing (data drift)
        // Given: Ownership for (email, orderId) is present; the corresponding Order cannot be retrieved
        // When: The user tracks their order
        // Then: The flow returns NotFound (the system cannot present details without the order)
        [TestMethod]
        public async Task Tracking_fails_when_order_missing_even_if_ownership_exists()
        {
            var email = "carol@example.com";
            var tenant = Guid.NewGuid();
            var orderId = 30303L;
            var ownership = BuildOwnership(email, orderId, tenant);

            var ownershipRepo = new Mock<IRepository<Ownership>>(MockBehavior.Strict);
            _ = ownershipRepo
                .Setup(r => r.RetrieveAsync(
                    Ownership.BuildPartitionKey(ownership.Email),
                    Ownership.BuildRowKey(ownership.Order),
                    It.IsAny<IList<string>?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(ownership);

            var orderRepo = new Mock<IRepository<Order>>(MockBehavior.Strict);
            _ = orderRepo
                .Setup(r => r.RetrieveAsync(
                    Order.BuildPartitionKey(tenant),
                    Ownership.BuildRowKey(orderId),
                    It.IsAny<IList<string>?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((Order?)null);

            var flow = new TrackFlow(ownershipRepo.Object, orderRepo.Object);

            // When tracking is attempted
            Func<Task> act = async () => await flow.RunAsync(BuildTrackRequest(email, orderId), CancellationToken.None);

            // Then not-found is surfaced to the customer
            var ex = await act.Should().ThrowAsync<ApplicationException>();
            _ = ex.Which.ErrorCode.Should().Be(InternalError.NotFound);
        }

        // Scenario: Email formatting from the user should not block tracking
        // Given: The user types their email with spaces and mixed case
        // When: TrackFlow looks up Ownership
        // Then: It uses normalized email (trim + lower) so legitimate customers are not blocked by formatting
        [TestMethod]
        public async Task Email_is_normalized_when_finding_ownership()
        {
            var rawEmail = "  DAVE@Example.COM  ";
            var normalized = rawEmail.Trim().ToLowerInvariant();
            var tenant = Guid.NewGuid();
            var orderId = 40404L;
            var ownership = BuildOwnership(normalized, orderId, tenant);

            var ownershipRepo = new Mock<IRepository<Ownership>>(MockBehavior.Strict);
            _ = ownershipRepo
                .Setup(r => r.RetrieveAsync(
                    Ownership.BuildPartitionKey(normalized),
                    Ownership.BuildRowKey(orderId),
                    It.IsAny<IList<string>?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(ownership);

            var order = BuildOrder(tenant, Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(-2), OrderStatus.Created, ShippingStatus.Pending, "Wellington", null, "NZ",
                new[] { new CartItem { Listing = 1, Option = "std", Quantity = 1 } });
            var orderRepo = new Mock<IRepository<Order>>(MockBehavior.Strict);
            _ = orderRepo
                .Setup(r => r.RetrieveAsync(
                    Order.BuildPartitionKey(tenant),
                    Ownership.BuildRowKey(orderId),
                    It.IsAny<IList<string>?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(order);

            var flow = new TrackFlow(ownershipRepo.Object, orderRepo.Object);

            // When tracking using the raw email
            var request = BuildTrackRequest(rawEmail, orderId);
            _ = await flow.RunAsync(request, CancellationToken.None);

            // Then repository lookup used normalized email
            ownershipRepo.Verify(r => r.RetrieveAsync(
                Ownership.BuildPartitionKey(normalized),
                Ownership.BuildRowKey(orderId),
                It.IsAny<IList<string>?>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        // Scenario: Order must be read from the correct tenant as recorded by Ownership
        // Given: Ownership ties the order to a specific tenant
        // When: The order is looked up for tracking
        // Then: The repository key uses that tenant ensuring correct multi-tenant isolation
        [TestMethod]
        public async Task Order_is_fetched_using_ownerships_tenant()
        {
            var email = "eve@example.com";
            var tenant = Guid.NewGuid();
            var orderId = 50505L;
            var ownership = BuildOwnership(email, orderId, tenant);

            var ownershipRepo = new Mock<IRepository<Ownership>>(MockBehavior.Strict);
            _ = ownershipRepo
                .Setup(r => r.RetrieveAsync(
                    Ownership.BuildPartitionKey(email),
                    Ownership.BuildRowKey(orderId),
                    It.IsAny<IList<string>?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(ownership);

            var order = BuildOrder(tenant, Guid.NewGuid(), DateTimeOffset.UtcNow, OrderStatus.Created, ShippingStatus.Pending, "Christchurch", null, "NZ",
                new[] { new CartItem { Listing = 9, Option = "std", Quantity = 1 } });
            var orderRepo = new Mock<IRepository<Order>>(MockBehavior.Strict);
            _ = orderRepo
                .Setup(r => r.RetrieveAsync(
                    Order.BuildPartitionKey(tenant),
                    Ownership.BuildRowKey(orderId),
                    It.IsAny<IList<string>?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(order);

            var flow = new TrackFlow(ownershipRepo.Object, orderRepo.Object);

            // When tracking occurs
            _ = await flow.RunAsync(BuildTrackRequest(email, orderId), CancellationToken.None);

            // Then order is retrieved using tenant from ownership
            orderRepo.Verify(r => r.RetrieveAsync(
                Order.BuildPartitionKey(tenant),
                Ownership.BuildRowKey(orderId),
                It.IsAny<IList<string>?>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        // Scenario: Cart details should be presented back to the customer as they were ordered
        // Given: The order contains multiple items with specific options/quantities
        // When: The customer tracks their order
        // Then: The response preserves the cart details exactly for customer visibility
        [TestMethod]
        public async Task Cart_details_are_preserved_in_response()
        {
            var email = "frank@example.com";
            var tenant = Guid.NewGuid();
            var orderId = 60606L;
            var ownership = BuildOwnership(email, orderId, tenant);

            var cart = new[]
            {
                new CartItem{ Listing = 88, Option = "std", Quantity = 1 },
                new CartItem{ Listing = 99, Option = "pro", Quantity = 3 },
                new CartItem{ Listing = 77, Option = null, Quantity = 2 },
            };
            var order = BuildOrder(tenant, Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(-5), OrderStatus.Created, ShippingStatus.Pending, "Hamilton", null, "NZ", cart);

            var ownershipRepo = new Mock<IRepository<Ownership>>(MockBehavior.Strict);
            _ = ownershipRepo
                .Setup(r => r.RetrieveAsync(
                    Ownership.BuildPartitionKey(email),
                    Ownership.BuildRowKey(orderId),
                    It.IsAny<IList<string>?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(ownership);

            var orderRepo = new Mock<IRepository<Order>>(MockBehavior.Strict);
            _ = orderRepo
                .Setup(r => r.RetrieveAsync(
                    Order.BuildPartitionKey(tenant),
                    Ownership.BuildRowKey(orderId),
                    It.IsAny<IList<string>?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(order);

            var flow = new TrackFlow(ownershipRepo.Object, orderRepo.Object);

            // When tracking occurs
            var response = await flow.RunAsync(BuildTrackRequest(email, orderId), CancellationToken.None);

            // Then cart content matches what was ordered
            _ = response.Cart.Should().HaveCount(cart.Length);
            _ = response.Cart.Select(c => (c.Listing, c.Option, c.Quantity))
                .Should().BeEquivalentTo(cart.Select(c => (c.Listing, c.Option, c.Quantity)));
        }

        // Scenario: Tracking a cancelled order still shows its status and shipping info to the customer
        // Given: An owned order has been cancelled
        // When: The customer tracks the order
        // Then: The response reflects the current status and shipping state (no hidden surprises)
        [TestMethod]
        public async Task Tracking_a_cancelled_order_returns_current_status_and_shipping_info()
        {
            var email = "grace@example.com";
            var tenant = Guid.NewGuid();
            var orderId = 70707L;
            var ownership = BuildOwnership(email, orderId, tenant);

            var order = BuildOrder(
                tenant: tenant,
                customerId: Guid.NewGuid(),
                created: DateTimeOffset.UtcNow.AddDays(-3),
                status: OrderStatus.Cancelled,
                shippingStatus: ShippingStatus.NotApplicable,
                city: "Dunedin",
                state: null,
                country: "NZ",
                cart: new[] { new CartItem { Listing = 1, Option = "std", Quantity = 1 } });

            var ownershipRepo = new Mock<IRepository<Ownership>>(MockBehavior.Strict);
            _ = ownershipRepo
                .Setup(r => r.RetrieveAsync(
                    Ownership.BuildPartitionKey(email),
                    Ownership.BuildRowKey(orderId),
                    It.IsAny<IList<string>?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(ownership);

            var orderRepo = new Mock<IRepository<Order>>(MockBehavior.Strict);
            _ = orderRepo
                .Setup(r => r.RetrieveAsync(
                    Order.BuildPartitionKey(tenant),
                    Ownership.BuildRowKey(orderId),
                    It.IsAny<IList<string>?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(order);

            var flow = new TrackFlow(ownershipRepo.Object, orderRepo.Object);

            // When the customer tracks
            var response = await flow.RunAsync(BuildTrackRequest(email, orderId), CancellationToken.None);

            // Then the current status and shipping info are visible
            _ = response.Status.Should().Be(OrderStatus.Cancelled);
            _ = response.ShippingStatus.Should().Be(ShippingStatus.NotApplicable);
            _ = response.ShippingCity.Should().Be("Dunedin");
        }

        // Builders (readability helpers; mirror how the app composes model values from inputs)
        private static Ownership BuildOwnership(string email, long orderId, Guid tenant) => new()
        {
            Email = email.Trim().ToLowerInvariant(),
            Order = orderId,
            Tenant = tenant
        };

        private static Order BuildOrder(
            Guid tenant,
            Guid customerId,
            DateTimeOffset created,
            OrderStatus status,
            ShippingStatus shippingStatus,
            string city,
            string? state,
            string country,
            CartItem[] cart)
        {
            var order = new Order
            {
                Customer = customerId,
                Created = created,
                Status = (int)status,
                ShippingStatus = (int)shippingStatus,
                Tenant = tenant,
                Currency = "NZD",
                Culture = "en-NZ",
                TimeZone = "Pacific/Auckland",
                Consignee = "Test User",
                Email = "customer@example.com",
                ShippingAddressLine1 = "1 Queen St",
                ShippingCity = city,
                ShippingCountry = country,
                ShippingPostcode = "1010",
                BillingName = "Test User",
                BillingAddressLine1 = "1 Queen St",
                BillingCity = city,
                BillingCountry = country,
                BillingPostcode = "1010",
                GrandTotal = 1000,
                SubTotal = 900,
                ShippingCost = 50,
                Tax = 50,
                ShippingState = state
            };
            order.SetCart(cart);
            return order;
        }

        private static TrackRequest BuildTrackRequest(string email, long orderId) => new()
        {
            ID = Guid.NewGuid(),
            Email = email,
            Captcha = "dummy-captcha",
            Order = orderId,
        };
    }
}
