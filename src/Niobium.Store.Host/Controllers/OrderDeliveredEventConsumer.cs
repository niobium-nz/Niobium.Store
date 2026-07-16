using Dapr;
using Microsoft.AspNetCore.Mvc;
using Niobium.Messaging;

namespace Niobium.Store.Host.Controllers
{
    [ApiController]
    [Route(DaprComponents.MessageRoute)]
    public class OrderDeliveredEventConsumer(IExternalEventAdaptor<Order, OrderDeliveredEvent> adaptor) : ControllerBase
    {
        [Topic(DaprComponents.ServiceBusPubSub, QueueNames.OrderDeliveredEvent, enableRawPayload: true)]
        [HttpPost(QueueNames.OrderDeliveredEvent)]
        public async Task<IActionResult> ConsumeAsync(OrderDeliveredEvent message, CancellationToken cancellationToken)
        {
            await adaptor.OnEvent(message, cancellationToken);
            return this.NoContent();
        }
    }
}
