using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Niobium.Messaging;
using Niobium.Messaging.ServiceBus;
using Niobium.Platform.ServiceBus;

namespace Niobium.Store.Server
{
    internal class OrderDeliveredEventConsumer(
        IExternalEventAdaptor<Order, OrderDeliveredEvent> adaptor,
        ILogger<OrderDeliveredEventConsumer> logger)
    {
        [Function(nameof(OrderDeliveredEventConsumer))]
        public async Task Run(
            [ServiceBusTrigger(QueueNames.OrderDeliveredEvent)]
            ServiceBusReceivedMessage message,
            CancellationToken cancellationToken)
        {
            if (!message.TryParse(out OrderDeliveredEvent? evt, out string? rawBody))
            {
                logger.LogError($"Failed to parse message {message.MessageId}: {rawBody}");
                return;
            }

            await adaptor.OnEvent(evt, cancellationToken);
        }
    }
}
