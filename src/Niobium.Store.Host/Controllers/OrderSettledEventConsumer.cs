using Dapr;
using Microsoft.AspNetCore.Mvc;
using Niobium.Messaging;

namespace Niobium.Store.Host.Controllers
{
    [ApiController]
    [Route(DaprComponents.MessageRoute)]
    public class OrderSettledEventConsumer(IExternalEventAdaptor<Order, OrderSettledEvent> adaptor) : ControllerBase
    {
        [Topic(DaprComponents.ServiceBusPubSub, QueueNames.OrderSettledEvent, enableRawPayload: true)]
        [HttpPost(QueueNames.OrderSettledEvent)]
        public async Task<IActionResult> ConsumeAsync(OrderSettledEvent message, CancellationToken cancellationToken)
        {
            await adaptor.OnEvent(message, cancellationToken);
            return this.NoContent();
        }
    }
}
