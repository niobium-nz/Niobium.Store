using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Niobium.Messaging;
using Niobium.Messaging.ServiceBus;
using Niobium.Platform.ServiceBus;

namespace Niobium.Store.Server
{
    internal class UpdateTrackingCommandConsumer(
        IExternalEventAdaptor<Order, UpdateTrackingCommand> adaptor,
        ILogger<UpdateTrackingCommandConsumer> logger)
    {
        [Function(nameof(UpdateTrackingCommandConsumer))]
        public async Task Run(
            [ServiceBusTrigger(QueueNames.UpdateTrackingCommand)]
            ServiceBusReceivedMessage message,
            CancellationToken cancellationToken)
        {
            if (!message.TryParse(out UpdateTrackingCommand? cmd, out string? rawBody))
            {
                logger.LogError($"Failed to parse message {message.MessageId}: {rawBody}");
                return;
            }

            await adaptor.OnEvent(cmd, cancellationToken);
        }
    }
}
