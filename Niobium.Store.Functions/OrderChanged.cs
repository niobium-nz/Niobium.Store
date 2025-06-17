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
            [ServiceBusTrigger("entitychangedevent-order", AutoCompleteMessages = true, Connection = nameof(ServiceBusOptions))]
            ServiceBusReceivedMessage message,
            CancellationToken cancellationToken)
        {
            if (!message.TryParse<EntityChangedEvent<Order>>(out var request))
            {
                logger.LogError("Failed to parse message: {MessageId}", message.MessageId);
                return;
            }

            await adaptor.OnEvent(request.Value, cancellationToken);
        }
    }
}
