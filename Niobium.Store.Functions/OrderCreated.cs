using Azure.Messaging.ServiceBus;
using Cod.Messaging;
using Cod.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Niobium.Store.Functions
{
    internal class OrderCreated(
        IExternalEventAdaptor<Order, OrderCreatedEvent> adaptor,
        ILogger<OrderCreated> logger)
    {
        [Function(nameof(OrderCreated))]
        public async Task Run(
            [ServiceBusTrigger("ordercreatedevent", AutoCompleteMessages = true, Connection = nameof(ServiceBusOptions))]
            ServiceBusReceivedMessage message,
            CancellationToken cancellationToken)
        {
            if (!message.TryParse<OrderCreatedEvent>(out var request))
            {
                logger.LogError("Failed to parse message: {MessageId}", message.MessageId);
                return;
            }

            await adaptor.OnEvent(request.Value, cancellationToken);
        }
    }
}
