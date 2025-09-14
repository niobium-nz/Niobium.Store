using Microsoft.Extensions.Logging;
using Niobium.Finance;

namespace Niobium.Store.Domains
{
    public class ShippingOptionDomain(
        Lazy<IRepository<ShippingOption>> repository,
        IEnumerable<IDomainEventHandler<IDomain<ShippingOption>>> eventHandlers,
        ILogger<ShippingOptionDomain> logger)
        : GenericDomain<ShippingOption>(repository, eventHandlers)
    {
        public async Task<TaxableAmount> QuoteAsync(QuoteRequest request, Tax tax, CancellationToken cancellationToken = default)
        {
            await this.ValidateAsync(request.ShippingCountry, cancellationToken);
            var entity = await this.GetEntityAsync(cancellationToken);
            return new TaxableAmount
            {
                Amount = new Amount { Cents = entity.Price, Currency = entity.Currency },
                Tax = tax,
            };
        }

        private async Task ValidateAsync(string countryCode, CancellationToken cancellationToken = default)
        {
            if (String.IsNullOrWhiteSpace(countryCode) || !Country.TryParse(countryCode, out var country))
            {
                throw new ArgumentException($"Invalid '{nameof(countryCode)}'.", nameof(countryCode));
            }

            var isShippingOptionSupported = false;
            var entity = await this.GetEntityAsync(cancellationToken);
            var supportedCountries = entity.GetCountries();
            foreach (var supportedCountry in supportedCountries)
            {
                if (!Country.TryParse(supportedCountry, out var c))
                {
                    logger.LogWarning($"Invalid country code on shipping option {entity.ID}: {supportedCountry}");
                    continue;
                }

                if (c == country)
                {
                    isShippingOptionSupported = true;
                    break;
                }
            }

            if (!isShippingOptionSupported)
            {
                var error = $"{country} is not supported by shipping option '{countryCode}'";
                logger.LogError(error);
                throw new ApplicationException(InternalError.BadRequest, error) { Reference = error };
            }
        }
    }
}
