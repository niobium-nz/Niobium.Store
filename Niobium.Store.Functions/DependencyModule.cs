using Cod.Database.StorageTable;
using Cod.Platform.Captcha.ReCaptcha;
using Cod.Platform.Finance;
using Cod.Platform.Finance.Stripe;
using Cod.Platform.StorageTable;
using Cod.Table.StorageAccount;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Niobium.Store.Functions
{
    public static class DependencyModule
    {
        private static volatile bool loaded;

        public static void AddStore(this IHostApplicationBuilder builder)
        {
            if (loaded)
            {
                return;
            }

            loaded = true;

            builder.AddFinance();
            builder.AddDatabase();
            builder.AddCaptcha();

            builder.Services.AddTransient(typeof(CloudTableRepository<>));
            builder.Services.AddMemoryCachedRepository<Listing>();
            builder.Services.AddMemoryCachedRepository<ShippingOption>();
        }
    }
}
