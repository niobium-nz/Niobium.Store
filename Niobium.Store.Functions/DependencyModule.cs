using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Niobium.Database.StorageTable;
using Niobium.Invoicing;
using Niobium.Messaging.ServiceBus;
using Niobium.Notification;
using Niobium.Platform;
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
            bool isDevelopment = builder.Configuration.IsDevelopmentEnvironment();

            builder.AddFinance();
            builder.AddDatabase();
            builder.AddMessaging();
            builder.AddCaptcha();

            _ = builder.Services.Configure<StoreOptions>(o => builder.Configuration.GetSection(nameof(StoreOptions)).Bind(o));
            builder.AddCore();

            _ = builder.Services.AddMemoryCachedRepository<Listing>();
            _ = builder.Services.AddMemoryCachedRepository<ShippingOption>();
            _ = builder.Services.AddMessagingBroker<SubscribeCommand>(isDevelopment, builder.Configuration.GetSection(nameof(NotificationQueueOptions)).Bind);
            _ = builder.Services.AddMessagingBroker<NotifyCommand>(isDevelopment, builder.Configuration.GetSection(nameof(NotificationQueueOptions)).Bind);
            _ = builder.Services.AddMessagingBroker<IssueInvoiceCommand>(isDevelopment, builder.Configuration.GetSection(nameof(InvoiceQueueOptions)).Bind);
        }
    }
}
