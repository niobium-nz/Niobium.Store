using Cod;
using Microsoft.Extensions.Logging;

namespace Niobium.Store
{
    public class ShippingOptionDomain(
        Lazy<IRepository<ShippingOption>> repository,
        IEnumerable<IDomainEventHandler<IDomain<ShippingOption>>> eventHandlers,
        ILogger<ShippingOptionDomain> logger)
        : GenericDomain<ShippingOption>(repository, eventHandlers)
    {
        public async Task<Country> FigureCountryAsync(string input, CancellationToken cancellationToken = default)
        {
            if (String.IsNullOrWhiteSpace(input) || !Country.TryParse(input, out var country))
            {
                throw new ArgumentException($"Invalid '{nameof(input)}'.", nameof(input));
            }

            var isShippingOptionSupported = false;
            var entity = await this.GetEntityAsync(cancellationToken);
            var supportedCountries = entity.GetCountries();
            foreach (var supportedCountry in supportedCountries)
            {
                if (!Country.TryParse(supportedCountry, out var c))
                {
                    logger.LogWarning($"Invalid country code on shipping option {entity.ID}: {supportedCountry}");
                }

                if (c == country)
                {
                    isShippingOptionSupported = true;
                    break;
                }
            }

            if (!isShippingOptionSupported)
            {
                var error = $"{country} is not supported by shipping option '{input}'";
                logger.LogWarning(error);
                throw new Cod.ApplicationException(InternalError.BadRequest, error) { Reference = error };
            }

            return country;
        }
    }
}
