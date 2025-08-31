using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Niobium.Messaging;
using Niobium.Platform.Finance;
using Niobium.Store.Domains;
using Niobium.Store.Options;
using Niobium.Store.Payment;

namespace Niobium.Store
{
    public static class DependencyModule
    {
        private static volatile bool loaded;

        public static void AddCore(this IFunctionsWorkerApplicationBuilder builder, Action<StoreInvoicingOptions>? storeInvoicingOptions)
        {
            if (loaded)
            {
                return;
            }

            loaded = true;

            _ = builder.Services.Configure<StoreInvoicingOptions>(o => storeInvoicingOptions?.Invoke(o));
            _ = builder.UsePlatformPayment<CustomerDepositRecorder, CustomerDomain, Customer>();
            _ = builder.Services.RegisterDomainComponents(typeof(DependencyModule));
            _ = builder.Services.EnableExternalEvent<OrderCreatedEvent, Order>();
            _ = builder.Services.EnableExternalEvent<OrderSettledEvent, Order>();
        }
    }
}
