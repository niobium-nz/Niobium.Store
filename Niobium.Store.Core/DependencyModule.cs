using AutoMapper;
using Cod;
using Cod.Platform.Finance;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Niobium.Store
{
    public static class DependencyModule
    {
        private static volatile bool loaded;
        private static MapperConfiguration mapperConfiguration = new(cfg =>
        {
            cfg.CreateMap<Order, OrderResponse>();
        });

        public static void AddCore(this IHostApplicationBuilder builder)
        {
            if (loaded)
            {
                return;
            }

            loaded = true;

            builder.Services.AddTransient(sp => mapperConfiguration.CreateMapper());
            builder.Services.AddDomain<OrderDomain, Order>();
            builder.Services.AddDomain<CustomerDomain, Customer>();
            builder.Services.AddDomain<ShippingOptionDomain, ShippingOption>();
            builder.Services.AddDomainEventHandler<OrderSettler, Transaction>();
        }
    }
}
