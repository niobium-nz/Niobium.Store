using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Niobium.Database.StorageTable;
using Niobium.Messaging.ServiceBus;
using Niobium.Notification;
using Niobium.Platform.Captcha.ReCaptcha;
using Niobium.Platform.Finance;
using Niobium.Platform.Finance.Stripe;
using Niobium.Platform.StorageTable;

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
