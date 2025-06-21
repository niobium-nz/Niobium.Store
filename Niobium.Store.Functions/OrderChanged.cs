using Azure.Messaging.ServiceBus;
using Cod;
using Cod.Messaging;
using Cod.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Niobium.Store.Functions
{
    internal class OrderChanged(
        IExternalEventAdaptor<Order, EntityChangedEvent<Order>> adaptor,
        ILogger<OrderChanged> logger)
    {
        [Function(nameof(OrderChanged))]
        public async Task Run(
            [ServiceBusTrigger("ordercreated", AutoCompleteMessages = true, Connection = nameof(ServiceBusOptions))]
            ServiceBusReceivedMessage message,
            CancellationToken cancellationToken)
        {
            if (!message.TryParse(out EntityChangedEvent<Order>? evt, out var rawBody))
            {
                logger.LogError($"Failed to parse message {message.MessageId}: {rawBody}");
                return;
            }

            await adaptor.OnEvent(evt, cancellationToken);
        }
    }
}
