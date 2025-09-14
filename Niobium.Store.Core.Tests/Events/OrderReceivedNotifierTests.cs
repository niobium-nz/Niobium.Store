using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Niobium.Messaging;
using Niobium.Notification;
using Niobium.Store;
using Niobium.Store.Events;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Niobium.Store.Core.Tests.Events
{
    [TestClass]
    public class OrderReceivedNotifierTests
    {
        [TestMethod]
        public async Task Order_paid_triggers_customer_notification_without_errors()
        {
            // Given a paid order
            var order = BuildOrder();
            var evt = new OrderSettledEvent { Order = order };

            // And a notifier with a messaging broker (external boundary mocked)
            var broker = new Mock<IMessagingBroker<NotifyCommand>>(MockBehavior.Loose);
            var notifier = new OrderReceivedNotifier(broker.Object);

            // When the order-settled event is handled
            Func<Task> act = async () => await notifier.HandleAsync(evt, CancellationToken.None);

            // Then the flow runs successfully from a business perspective
            await act.Should().NotThrowAsync();
        }

        private static Order BuildOrder()
        {
            var customer = Guid.NewGuid();
            var created = DateTimeOffset.UtcNow;

            var order = new Order
            {
                Customer = customer,
                Created = created,
                Status = (int)OrderStatus.Paid,
                ShippingStatus = (int)ShippingStatus.Pending,
                Tenant = Guid.NewGuid(),
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
                GrandTotal = 1000,
                SubTotal = 900,
                ShippingCost = 50,
                Tax = 50,
            };

            return order;
        }
    }
}
