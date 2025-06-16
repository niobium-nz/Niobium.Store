using Azure.Messaging.ServiceBus;
using Cod.Messaging;
using Cod.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Niobium.Store.Functions
{
    internal class OrderUpdated(
        IExternalEventAdaptor<Order, OrderUpdatedEvent> adaptor,
        ILogger<OrderUpdated> logger)
    {
        [Function(nameof(OrderUpdated))]
        public async Task Run(
            [ServiceBusTrigger("orderupdatedevent", AutoCompleteMessages = true, Connection = nameof(ServiceBusOptions))]
            ServiceBusReceivedMessage message,
            CancellationToken cancellationToken)
        {
            if (!message.TryParse<OrderUpdatedEvent>(out var request))
            {
                logger.LogError("Failed to parse message: {MessageId}", message.MessageId);
                return;
            }

            await adaptor.OnEvent(request.Value, cancellationToken);
        }
    }
}
