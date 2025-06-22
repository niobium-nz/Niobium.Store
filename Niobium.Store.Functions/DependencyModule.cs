using Cod.Database.StorageTable;
using Cod.Messaging.ServiceBus;
using Cod.Platform.Captcha.ReCaptcha;
using Cod.Platform.Finance;
using Cod.Platform.Finance.Stripe;
using Cod.Platform.ServiceBus;
using Cod.Platform.StorageTable;
using Cod.Table.StorageAccount;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Niobium.Notification;

namespace Niobium.Store.Functions
{
    public static class DependencyModule
    {
        private static volatile bool loaded;

        public static void AddStore(this FunctionsApplicationBuilder builder)
        {
            if (loaded)
            {
                return;
            }

            loaded = true;

            builder.AddFinance();
            builder.AddDatabase();
            builder.AddMessaging();
            builder.AddCaptcha();
            builder.AddCore();

            builder.Services.AddTransient(typeof(CloudTableRepository<>));
            builder.Services.AddMemoryCachedRepository<Listing>();
            builder.Services.AddMemoryCachedRepository<ShippingOption>();

            builder.Services.AddMessagingBroker<SubscribeCommand>(builder.Configuration.GetSection(nameof(SubscriptionServiceBusOptions)).Bind);
        }
    }
}
