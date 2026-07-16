using Microsoft.Extensions.Hosting;
using Niobium.Messaging;
using Niobium.Platform.Finance;

namespace Niobium.Store
{
    public static class DependencyModule
    {
        private static volatile bool loaded;

        public static void AddCore(this IHostApplicationBuilder builder)
        {
            if (loaded)
            {
                return;
            }

            loaded = true;

            builder.Services.RegisterDomainComponents(typeof(DependencyModule));
            builder.Services.EnableExternalEvent<OrderCreatedEvent, Order>();
            builder.Services.EnableExternalEvent<OrderSettledEvent, Order>();
            builder.Services.EnableExternalEvent<OrderShippedEvent, Order>();
            builder.Services.EnableExternalEvent<OrderDeliveredEvent, Order>();
            builder.Services.EnableExternalEvent<UpdateTrackingCommand, Order>();
        }
    }
}
