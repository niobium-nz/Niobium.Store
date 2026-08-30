using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Niobium.Messaging;
using Niobium.Messaging.ServiceBus;
using Niobium.Platform.ServiceBus;

namespace Niobium.Store.Server
{
    internal class OrderSettledEventConsumer(
        IExternalEventAdaptor<Order, OrderSettledEvent> adaptor,
        ILogger<OrderSettledEventConsumer> logger)
    {
        [Function(nameof(OrderSettledEventConsumer))]
        public async Task Run(
            [ServiceBusTrigger(QueueNames.OrderSettledEvent)]
            ServiceBusReceivedMessage message,
            CancellationToken cancellationToken)
        {
            if (!message.TryParse(out OrderSettledEvent? evt, out string? rawBody))
            {
                logger.LogError($"Failed to parse message {message.MessageId}: {rawBody}");
                return;
            }

            await adaptor.OnEvent(evt, cancellationToken);
        }
    }
}
