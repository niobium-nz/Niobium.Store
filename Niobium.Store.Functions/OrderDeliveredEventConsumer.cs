using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Niobium.Messaging;
using Niobium.Messaging.ServiceBus;
using Niobium.Platform.ServiceBus;
using Niobium.Store.Functions.Options;

namespace Niobium.Store.Functions
{
    internal class OrderDeliveredEventConsumer(
        IExternalEventAdaptor<Order, OrderDeliveredEvent> adaptor,
        ILogger<OrderDeliveredEventConsumer> logger)
    {
        [Function(nameof(OrderDeliveredEventConsumer))]
        public async Task Run(
            [ServiceBusTrigger("orderdeliveredevent", AutoCompleteMessages = true, Connection = nameof(ServiceBusTriggerOptions))]
            ServiceBusReceivedMessage message,
            CancellationToken cancellationToken)
        {
            if (!message.TryParse(out OrderDeliveredEvent? evt, out var rawBody))
            {
                logger.LogError($"Failed to parse message {message.MessageId}: {rawBody}");
                return;
            }

            await adaptor.OnEvent(evt, cancellationToken);
        }
    }
}
