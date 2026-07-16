using Dapr;
using Microsoft.AspNetCore.Mvc;
using Niobium.Messaging;

namespace Niobium.Store.Host.Controllers
{
    [ApiController]
    [Route(DaprComponents.MessageRoute)]
    public class UpdateTrackingCommandConsumer(IExternalEventAdaptor<Order, UpdateTrackingCommand> adaptor) : ControllerBase
    {
        [Topic(DaprComponents.ServiceBusPubSub, QueueNames.UpdateTrackingCommand, enableRawPayload: true)]
        [HttpPost(QueueNames.UpdateTrackingCommand)]
        public async Task<IActionResult> ConsumeAsync(UpdateTrackingCommand message, CancellationToken cancellationToken)
        {
            await adaptor.OnEvent(message, cancellationToken);
            return this.NoContent();
        }
    }
}
