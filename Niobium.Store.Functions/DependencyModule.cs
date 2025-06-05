using Cod.Database.StorageTable;
using Cod.Platform;
using Cod.Platform.Captcha;
using Cod.Platform.Captcha.Recaptcha;
using Cod.Platform.Finance.Stripe;
using Cod.Platform.StorageTable;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Niobium.Store.Functions
{
    public static class DependencyModule
    {
        private static volatile bool loaded;

        public static IServiceCollection AddStore(this IServiceCollection services, IConfiguration configuration)
        {
            if (loaded)
            {
                return services;
            }

            loaded = true;

            var isDevelopment = configuration.IsDevelopmentEnvironment();
            return services.AddFinance(configuration.GetRequiredSection(nameof(PaymentServiceOptions)))
                .AddDatabase(configuration.GetRequiredSection(nameof(StorageTableOptions)))
                    .PostConfigure<StorageTableOptions>(opt => opt.EnableInteractiveIdentity = isDevelopment)
                    .AddMemoryCachedRepository<Listing>()
                .AddCaptcha(configuration.GetRequiredSection(nameof(CaptchaOptions)));
        }
    }
}
