using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Niobium.Database.StorageTable;
using Niobium.Invoicing;
using Niobium.Messaging.ServiceBus;
using Niobium.Notification;
using Niobium.Platform;
using Niobium.Platform.Captcha.ReCaptcha;
using Niobium.Platform.Finance;
using Niobium.Platform.Finance.Stripe;
using Niobium.Platform.Functions;
using Niobium.Platform.ServiceBus;
using Niobium.Platform.StorageTable;
using Niobium.Store.Options;
using Niobium.Store.Server.Options;

namespace Niobium.Store.Server
{
    internal static class DependencyModule
    {
        private static volatile bool added;
        private static volatile bool used;

        public static TBuilder AddStore<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
            => builder.AddStore(builder.Configuration.GetSection(nameof(StoreOptions)).Bind);

        public static TBuilder AddStore<TBuilder>(this TBuilder builder, Action<StoreOptions>? options)
             where TBuilder : IHostApplicationBuilder
        {
            if (added)
            {
                return builder;
            }

            added = true;

            Platform.Functions.DependencyModule.AddPlatform(builder);
            builder.AddFinance();
            builder.AddDatabase();
            builder.AddMessaging();
            builder.AddCaptcha();
            builder.AddCore();

            builder.Services.Configure<StoreOptions>(o => options?.Invoke(o));

            bool isDevEnv = builder.Configuration.IsDevelopmentEnvironment();
            builder.Services.AddMemoryCachedRepository<Listing>();
            builder.Services.AddMemoryCachedRepository<ShippingOption>();
            builder.Services.AddMessagingBroker<SubscribeCommand>(isDevEnv, builder.Configuration.GetSection(nameof(NotificationQueueOptions)).Bind);
            builder.Services.AddMessagingBroker<NotifyCommand>(isDevEnv, builder.Configuration.GetSection(nameof(NotificationQueueOptions)).Bind);
            builder.Services.AddMessagingBroker<IssueInvoiceCommand>(isDevEnv, builder.Configuration.GetSection(nameof(InvoiceQueueOptions)).Bind);
            builder.Services.AddTransient<IRepository<QuantifiedListing>>(sp =>
            {
                CloudTableRepository<QuantifiedListing> repo = sp.GetRequiredService<CloudTableRepository<QuantifiedListing>>();
                repo.TableName = nameof(Listing);
                return repo;
            });
            return builder;
        }

        public static TBuilder UseStore<TBuilder>(this TBuilder builder) where TBuilder : IFunctionsWorkerApplicationBuilder
        {
            if (used)
            {
                return builder;
            }

            used = true;

            builder.ToMiddlewareHost().UsePlatformPayment();
            return builder;
        }
    }
}
