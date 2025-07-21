using AutoMapper;
using Cod;
using Cod.Finance;
using Cod.Messaging;
using Cod.Platform.Finance;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

            builder.UsePlatformPayment<CustomerDepositRecorder, CustomerDomain, Customer>();
            builder.Services.AddSingleton(sp => new MapperConfiguration(ConfigureMapping, sp.GetRequiredService<ILoggerFactory>()));
            builder.Services.AddTransient(sp => sp.GetRequiredService<MapperConfiguration>().CreateMapper());
            builder.Services.AddDomain<OrderDomain, Order>();
            builder.Services.AddDomain<CustomerDomain, Customer>();
            builder.Services.AddDomain<ShippingOptionDomain, ShippingOption>();
            builder.Services.AddDomainEventHandler<OrderSettler, Transaction>();
            builder.Services.AddDomainEventHandler<CustomerCreator, Order>();
            builder.Services.AddDomainEventHandler<ReceiptIssuer, Order>();
            builder.Services.AddDomainEventHandler<OrderCreatedEventAdaptor, Order>();
            builder.Services.AddDomainEventHandler<OrderSettledEventAdaptor, Order>();
            builder.Services.AddDomainEventHandler<SubscriptionSynchronizer, Order>();
            builder.Services.EnableExternalEvent<OrderCreatedEvent, Order>();
            builder.Services.EnableExternalEvent<OrderSettledEvent, Order>();
        }

        private static void ConfigureMapping(IMapperConfigurationExpression cfg)
        {
            cfg.CreateMap<Order, OrderResponse>();
        }
    }
}
