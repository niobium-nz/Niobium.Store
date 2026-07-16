using Microsoft.AspNetCore.HttpOverrides;
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
using Niobium.Store.Host.Options;
using Niobium.Store.Options;

namespace Niobium.Store.Host
{
    internal static class DependencyModule
    {
        private static volatile bool loaded;

        public static WebApplicationBuilder AddStore(this WebApplicationBuilder builder) => builder.AddStore(builder.Configuration.GetSection(nameof(StoreOptions)).Bind);

        public static WebApplicationBuilder AddStore(this WebApplicationBuilder builder, Action<StoreOptions>? options)
        {
            if (loaded)
            {
                return builder;
            }

            loaded = true;
            bool isDevEnv = builder.Configuration.IsDevelopmentEnvironment();

            builder.Services.Configure<StoreOptions>(o => options?.Invoke(o));

            builder.Services.AddDaprClient();
            builder.Services.AddControllers().AddDapr();
            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
            });

            builder.ConfigureOpenTelemetry();

            builder.AddFinance();
            builder.AddDatabase();
            builder.AddMessaging();
            builder.AddCaptcha();
            builder.AddCore();

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

        public static WebApplication UseStore(this WebApplication app)
        {
            app.UseForwardedHeaders();
            app.UseRouting();
            app.UseCloudEvents();
            app.UsePlatformPayment();
            app.MapControllers();
            app.MapSubscribeHandler();
            return app;
        }
    }
}
