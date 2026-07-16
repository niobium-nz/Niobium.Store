using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Niobium.Messaging;
using Niobium.Notification;
using Niobium.Store;
using Niobium.Store.Events;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Niobium.Store.Core.Tests.Events
{
    [TestClass]
    public class SubscriptionSynchronizerTests
    {
        [TestMethod]
        public async Task Marketing_opt_in_runs_without_errors()
        {
            // Given an order created with marketing subscription opted in
            var order = BuildOrder(marketingSubscription: true, listingId: 1234, optionId: "standard");
            var evt = new OrderCreatedEvent { Order = order };

            var broker = new Mock<IMessagingBroker<SubscribeCommand>>(MockBehavior.Loose);
            var sync = new SubscriptionSynchronizer(broker.Object);

            // When handling the order-created event
            Func<Task> act = async () => await sync.HandleAsync(evt, CancellationToken.None);

            // Then the business flow completes without throwing
            await act.Should().NotThrowAsync();
        }

        [TestMethod]
        public async Task Marketing_not_opt_in_runs_without_errors_and_no_enqueue_calls()
        {
            // Given an order created without marketing subscription
            var order = BuildOrder(marketingSubscription: false, listingId: 5678, optionId: "standard");
            var evt = new OrderCreatedEvent { Order = order };

            var broker = new Mock<IMessagingBroker<SubscribeCommand>>(MockBehavior.Loose);
            var sync = new SubscriptionSynchronizer(broker.Object);

            // When handling the order-created event
            Func<Task> act = async () => await sync.HandleAsync(evt, CancellationToken.None);
            await act.Should().NotThrowAsync();

            // And from a business perspective, no subscription message should be produced
            broker.Invocations.Should().BeEmpty();
        }

        private static Order BuildOrder(bool marketingSubscription, int listingId, string optionId)
        {
            var customer = Guid.NewGuid();
            var created = DateTimeOffset.UtcNow;

            var cart = new[]
            {
                new CartItem
                {
                    Listing = listingId,
                    Option = optionId,
                    Quantity = 1,
                }
            };

            var order = new Order
            {
                Customer = customer,
                Created = created,
                Status = (int)OrderStatus.Created,
                ShippingStatus = (int)ShippingStatus.NotApplicable,
                Tenant = Guid.NewGuid(),
                Currency = "USD",
                Culture = "en-US",
                TimeZone = "UTC",
                Consignee = "John Doe",
                Email = "john@example.com",
                ShippingAddressLine1 = "1 Queen St",
                ShippingCity = "Auckland",
                ShippingCountry = "NZ",
                ShippingPostcode = "1010",
                BillingName = "John Doe",
                BillingAddressLine1 = "1 Queen St",
                BillingCity = "Auckland",
                BillingCountry = "NZ",
                BillingPostcode = "1010",
                ShippingCost = 0,
                Tax = 50,
                MarketingSubscription = marketingSubscription,
                Track = "abc123"
            };

            order.SetCart(cart);
            return order;
        }
    }
}
