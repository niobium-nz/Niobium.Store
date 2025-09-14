using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Niobium.Store;
using Niobium.Store.Flows;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Niobium.Store.Core.Tests.Flows
{
    [TestClass]
    public class TrackFlowTests
    {
        // BUSINESS CASE: Customer can track an order they own and see the order details
        [TestMethod]
        public async Task Tracking_an_owned_order_returns_order_details()
        {
            // Given an ownership exists for the customer's email and order
            var email = "alice@example.com";
            var tenant = Guid.NewGuid();
            var orderId = 10101L;
            var ownership = BuildOwnership(email, orderId, tenant);

            // And the corresponding order exists with shipping and cart details
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
            ownershipRepo
                .Setup(r => r.RetrieveAsync(
                    Ownership.BuildPartitionKey(ownership.Email),
                    Ownership.BuildRowKey(ownership.Order),
                    It.IsAny<IList<string>?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(ownership);

            var orderRepo = new Mock<IRepository<Order>>(MockBehavior.Strict);
            orderRepo
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
            response.Created.Should().Be(created);
            response.Status.Should().Be(OrderStatus.Created);
            response.ShippingStatus.Should().Be(ShippingStatus.Pending);
            response.ShippingCity.Should().Be("Auckland");
            response.ShippingState.Should().BeNull();
            response.ShippingCountry.Should().NotBeNull();
            response.Cart.Should().HaveCount(2);
            response.Cart.First().Listing.Should().Be(2001);
            response.Cart.Last().Quantity.Should().Be(2);
        }

        // BUSINESS CASE: Tracking fails when this email never placed the order
        [TestMethod]
        public async Task Tracking_fails_when_no_ownership_exists()
        {
            // Given no ownership record exists for the provided email/order
            var email = "bob@example.com";
            var orderId = 20202L;

            var ownershipRepo = new Mock<IRepository<Ownership>>(MockBehavior.Strict);
            ownershipRepo
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
            ex.Which.ErrorCode.Should().Be(InternalError.NotFound);
        }

        // BUSINESS CASE: Tracking fails if the order record is missing (data drift)
        [TestMethod]
        public async Task Tracking_fails_when_order_missing_even_if_ownership_exists()
        {
            // Given an ownership exists but the order record is missing
            var email = "carol@example.com";
            var tenant = Guid.NewGuid();
            var orderId = 30303L;
            var ownership = BuildOwnership(email, orderId, tenant);

            var ownershipRepo = new Mock<IRepository<Ownership>>(MockBehavior.Strict);
            ownershipRepo
                .Setup(r => r.RetrieveAsync(
                    Ownership.BuildPartitionKey(ownership.Email),
                    Ownership.BuildRowKey(ownership.Order),
                    It.IsAny<IList<string>?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(ownership);

            var orderRepo = new Mock<IRepository<Order>>(MockBehavior.Strict);
            orderRepo
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
            ex.Which.ErrorCode.Should().Be(InternalError.NotFound);
        }

        // BUSINESS CASE: Email normalization (spaces/casing) does not block tracking
        [TestMethod]
        public async Task Email_is_normalized_when_finding_ownership()
        {
            // Given the user typed email with spaces and mixed case
            var rawEmail = "  DAVE@Example.COM  ";
            var normalized = rawEmail.Trim().ToLowerInvariant();
            var tenant = Guid.NewGuid();
            var orderId = 40404L;
            var ownership = BuildOwnership(normalized, orderId, tenant);

            var ownershipRepo = new Mock<IRepository<Ownership>>(MockBehavior.Strict);
            ownershipRepo
                .Setup(r => r.RetrieveAsync(
                    Ownership.BuildPartitionKey(normalized),
                    Ownership.BuildRowKey(orderId),
                    It.IsAny<IList<string>?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(ownership);

            var order = BuildOrder(tenant, Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(-2), OrderStatus.Created, ShippingStatus.Pending, "Wellington", null, "NZ",
                new[] { new CartItem { Listing = 1, Option = "std", Quantity = 1 } });
            var orderRepo = new Mock<IRepository<Order>>(MockBehavior.Strict);
            orderRepo
                .Setup(r => r.RetrieveAsync(
                    Order.BuildPartitionKey(tenant),
                    Ownership.BuildRowKey(orderId),
                    It.IsAny<IList<string>?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(order);

            var flow = new TrackFlow(ownershipRepo.Object, orderRepo.Object);

            // When tracking using the raw email
            var request = BuildTrackRequest(rawEmail, orderId);
            var _ = await flow.RunAsync(request, CancellationToken.None);

            // Then repository lookup used normalized email
            ownershipRepo.Verify(r => r.RetrieveAsync(
                Ownership.BuildPartitionKey(normalized),
                Ownership.BuildRowKey(orderId),
                It.IsAny<IList<string>?>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        // BUSINESS CASE: Order is fetched under the correct tenant recorded by ownership
        [TestMethod]
        public async Task Order_is_fetched_using_ownerships_tenant()
        {
            // Given ownership records the tenant for this order
            var email = "eve@example.com";
            var tenant = Guid.NewGuid();
            var orderId = 50505L;
            var ownership = BuildOwnership(email, orderId, tenant);

            var ownershipRepo = new Mock<IRepository<Ownership>>(MockBehavior.Strict);
            ownershipRepo
                .Setup(r => r.RetrieveAsync(
                    Ownership.BuildPartitionKey(email),
                    Ownership.BuildRowKey(orderId),
                    It.IsAny<IList<string>?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(ownership);

            var order = BuildOrder(tenant, Guid.NewGuid(), DateTimeOffset.UtcNow, OrderStatus.Created, ShippingStatus.Pending, "Christchurch", null, "NZ",
                new[] { new CartItem { Listing = 9, Option = "std", Quantity = 1 } });
            var orderRepo = new Mock<IRepository<Order>>(MockBehavior.Strict);
            orderRepo
                .Setup(r => r.RetrieveAsync(
                    Order.BuildPartitionKey(tenant),
                    Ownership.BuildRowKey(orderId),
                    It.IsAny<IList<string>?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(order);

            var flow = new TrackFlow(ownershipRepo.Object, orderRepo.Object);

            // When tracking occurs
            var _ = await flow.RunAsync(BuildTrackRequest(email, orderId), CancellationToken.None);

            // Then order is retrieved using tenant from ownership
            orderRepo.Verify(r => r.RetrieveAsync(
                Order.BuildPartitionKey(tenant),
                Ownership.BuildRowKey(orderId),
                It.IsAny<IList<string>?>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        // BUSINESS CASE: Cart details are presented as-is for customer visibility
        [TestMethod]
        public async Task Cart_details_are_preserved_in_response()
        {
            // Given an order with multiple cart items
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
            ownershipRepo
                .Setup(r => r.RetrieveAsync(
                    Ownership.BuildPartitionKey(email),
                    Ownership.BuildRowKey(orderId),
                    It.IsAny<IList<string>?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(ownership);

            var orderRepo = new Mock<IRepository<Order>>(MockBehavior.Strict);
            orderRepo
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
            response.Cart.Should().HaveCount(cart.Length);
            response.Cart.Select(c => (c.Listing, c.Option, c.Quantity))
                .Should().BeEquivalentTo(cart.Select(c => (c.Listing, c.Option, c.Quantity)));
        }

        // BUSINESS CASE: Tracking a cancelled order still returns its current status and shipping info
        [TestMethod]
        public async Task Tracking_a_cancelled_order_returns_current_status_and_shipping_info()
        {
            // Given an owned order that has been cancelled
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
            ownershipRepo
                .Setup(r => r.RetrieveAsync(
                    Ownership.BuildPartitionKey(email),
                    Ownership.BuildRowKey(orderId),
                    It.IsAny<IList<string>?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(ownership);

            var orderRepo = new Mock<IRepository<Order>>(MockBehavior.Strict);
            orderRepo
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
            response.Status.Should().Be(OrderStatus.Cancelled);
            response.ShippingStatus.Should().Be(ShippingStatus.NotApplicable);
            response.ShippingCity.Should().Be("Dunedin");
        }

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
            };
            order.ShippingState = state;
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
