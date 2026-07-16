using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Niobium.Messaging;
using Niobium.Messaging.ServiceBus;
using Niobium.Platform.ServiceBus;
using Niobium.Store.Functions.Options;

namespace Niobium.Store.Functions
{
    internal class OrderShippedEventConsumer(
        IExternalEventAdaptor<Order, OrderShippedEvent> adaptor,
        ILogger<OrderShippedEventConsumer> logger)
    {
        [Function(nameof(OrderShippedEventConsumer))]
        public async Task Run(
            [ServiceBusTrigger("ordershippedevent", Connection = nameof(ServiceBusTriggerOptions))]
            ServiceBusReceivedMessage message,
            CancellationToken cancellationToken)
        {
            if (!message.TryParse(out OrderShippedEvent? evt, out var rawBody))
            {
                logger.LogError($"Failed to parse message {message.MessageId}: {rawBody}");
                return;
            }

            await adaptor.OnEvent(evt, cancellationToken);
        }
    }
}
