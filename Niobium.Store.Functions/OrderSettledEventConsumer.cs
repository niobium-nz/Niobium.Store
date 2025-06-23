using Azure.Messaging.ServiceBus;
using Cod.Messaging;
using Cod.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Niobium.Store.Functions
{
    internal class OrderSettledEventConsumer(
        IExternalEventAdaptor<Order, OrderSettledEvent> adaptor,
        ILogger<OrderSettledEventConsumer> logger)
    {
        [Function(nameof(OrderSettledEventConsumer))]
        public async Task Run(
            [ServiceBusTrigger("ordersettledevent", AutoCompleteMessages = true, Connection = nameof(ServiceBusOptions))]
            ServiceBusReceivedMessage message,
            CancellationToken cancellationToken)
        {
            if (!message.TryParse(out OrderSettledEvent? evt, out var rawBody))
            {
                logger.LogError($"Failed to parse message {message.MessageId}: {rawBody}");
                return;
            }

            await adaptor.OnEvent(evt, cancellationToken);
        }
    }
}
