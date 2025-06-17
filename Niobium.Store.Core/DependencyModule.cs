using AutoMapper;
using Cod;
using Cod.Messaging;
using Cod.Platform.Finance;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;

namespace Niobium.Store
{
    public static class DependencyModule
    {
        private static volatile bool loaded;
        private static readonly MapperConfiguration mapperConfiguration = new(cfg =>
        {
            cfg.CreateMap<Order, OrderResponse>();
        });

        public static void AddCore(this IFunctionsWorkerApplicationBuilder builder)
        {
            if (loaded)
            {
                return;
            }

            loaded = true;

            builder.UsePlatformPayment<CustomerDepositRecorder, CustomerDomain, Customer>();
            builder.Services.AddTransient(sp => mapperConfiguration.CreateMapper());
            builder.Services.AddDomain<OrderDomain, Order>();
            builder.Services.AddDomain<CustomerDomain, Customer>();
            builder.Services.AddDomain<ShippingOptionDomain, ShippingOption>();
            builder.Services.AddDomainEventHandler<OrderSettler, Transaction>();
            builder.Services.AddDomainEventHandler<CustomerCreator, Order>();
            builder.Services.AddDomainEventHandler<ReceiptIssuer, Order>();
            builder.Services.EnableExternalEvent<Order, EntityChangedEvent<Order>>();
        }
    }
}
