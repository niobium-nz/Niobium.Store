using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Niobium.Database.StorageTable;
using Niobium.Invoicing;
using Niobium.Messaging.ServiceBus;
using Niobium.Notification;
using Niobium.Platform.Captcha.ReCaptcha;
using Niobium.Platform.Finance;
using Niobium.Platform.Finance.Stripe;
using Niobium.Platform.ServiceBus;
using Niobium.Platform.StorageTable;
using Niobium.Store.Functions.Options;
using Niobium.Store.Options;

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

            _ = builder.Services.Configure<StoreOptions>(o => builder.Configuration.GetSection(nameof(StoreOptions)).Bind(o));
            builder.AddCore();

            _ = builder.Services.AddMemoryCachedRepository<Listing>();
            _ = builder.Services.AddMemoryCachedRepository<ShippingOption>();
            _ = builder.Services.AddMessagingBroker<SubscribeCommand>(builder.Configuration.GetSection(nameof(SubscribeQueueOptions)).Bind);
            _ = builder.Services.AddMessagingBroker<IssueInvoiceCommand>(builder.Configuration.GetSection(nameof(InvoiceQueueOptions)).Bind);
        }
    }
}
