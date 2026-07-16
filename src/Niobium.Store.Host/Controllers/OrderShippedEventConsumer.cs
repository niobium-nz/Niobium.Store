using Dapr;
using Microsoft.AspNetCore.Mvc;
using Niobium.Messaging;

namespace Niobium.Store.Host.Controllers
{
    [ApiController]
    [Route(DaprComponents.MessageRoute)]
    public class OrderShippedEventConsumer(IExternalEventAdaptor<Order, OrderShippedEvent> adaptor) : ControllerBase
    {
        [Topic(DaprComponents.ServiceBusPubSub, QueueNames.OrderShippedEvent, enableRawPayload: true)]
        [HttpPost(QueueNames.OrderShippedEvent)]
        public async Task<IActionResult> ConsumeAsync(OrderShippedEvent message, CancellationToken cancellationToken)
        {
            await adaptor.OnEvent(message, cancellationToken);
            return this.NoContent();
        }
    }
}
