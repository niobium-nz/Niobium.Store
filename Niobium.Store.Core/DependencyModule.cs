using Microsoft.Azure.Functions.Worker;
using Niobium.Messaging;
using Niobium.Platform.Finance;

namespace Niobium.Store
{
    public static class DependencyModule
    {
        private static volatile bool loaded;

        public static void AddCore(this IFunctionsWorkerApplicationBuilder builder)
        {
            if (loaded)
            {
                return;
            }

            loaded = true;

            _ = builder.UsePlatformPayment();
            _ = builder.Services.RegisterDomainComponents(typeof(DependencyModule));
            _ = builder.Services.EnableExternalEvent<OrderCreatedEvent, Order>();
            _ = builder.Services.EnableExternalEvent<OrderSettledEvent, Order>();
            _ = builder.Services.EnableExternalEvent<OrderShippedEvent, Order>();
            _ = builder.Services.EnableExternalEvent<OrderDeliveredEvent, Order>();
            _ = builder.Services.EnableExternalEvent<UpdateTrackingCommand, Order>();
        }
    }
}
