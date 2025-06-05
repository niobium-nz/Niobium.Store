using Cod;
using Cod.Platform;
using Cod.Platform.Captcha.Recaptcha;
using Cod.Platform.Finance;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using static System.Runtime.InteropServices.JavaScript.JSType;
using InternalError = Cod.Platform.InternalError;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Niobium.Store.Functions;

public class MakeOrder(
    IRepository<Listing> listingRepo,
    IRepository<ShippingOption> shippingRepo,
    IRepository<Order> orderRepo,
    IVisitorRiskAssessor assessor,
    ILogger<MakeOrder> logger)
{
    private static readonly JsonSerializerOptions serializationOptions = new(JsonSerializerDefaults.Web);

    [Function(nameof(MakeOrder))]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "orders")] HttpRequest req, CancellationToken cancellationToken)
    {
        var request = await JsonSerializer.DeserializeAsync<OrderRequest>(req.Body, options: serializationOptions, cancellationToken: cancellationToken);
        ArgumentNullException.ThrowIfNull(request);

        Country country = default;
        ValidationState validationState = new();
        if (request.TryValidate(out validationState))
        {
            if (!Country.TryParse(request.ShippingCountry, out country))
            {
                validationState.AddError(nameof(request.ShippingCountry), $"Invalid country code: {request.ShippingCountry}");
            }
        }

        if (!validationState.IsValid)
        {
            logger.LogWarning("Validation failed for order request: {Errors}", JsonSerializer.Serialize(validationState.ToDictionary(), serializationOptions));
            return validationState.MakeResponse();
        }

        if (request.Cart.Count == 0)
        {
            var error = $"No valid listings found from the order: {request.ID}";
            logger.LogWarning(error);
            throw new Cod.ApplicationException(InternalError.BadRequest, error) { Reference = "No valid listings found from your order." };
        }

        var clientIP = req.GetRemoteIP();
        var lowRisk = await assessor.AssessAsync(request.ID, request.Captcha, clientIP, cancellationToken);
        if (!lowRisk)
        {
            var error = $"{clientIP} is considered high risk for order: {request.ID}";
            logger.LogWarning(error);
            throw new Cod.ApplicationException(InternalError.Forbidden, error);
        }

        var shippingOption = await shippingRepo.RetrieveAsync(ShippingOption.BuildPartitionKey(), ShippingOption.BuildRowKey(request.Shipping), cancellationToken: cancellationToken);
        if (shippingOption is null)
        {
            var error = $"Invalid shipping option: {request.Shipping}";
            logger.LogWarning(error);
            throw new Cod.ApplicationException(InternalError.BadRequest, error) { Reference = error };
        }

        var isShippingOptionSupported = false;
        var supportedCountries = shippingOption.GetCountries();
        foreach (var supportedCountry in supportedCountries)
        {
            if (!Country.TryParse(supportedCountry, out var c))
            {
                logger.LogWarning($"Invalid country code on shipping option {request.Shipping}: {supportedCountry}");
            }

            if (c == country)
            {
                isShippingOptionSupported = true;
                break;
            }
        }
        if (!isShippingOptionSupported)
        {
            var error = $"{country} is not supported by shipping option '{shippingOption.Name}'";
            logger.LogWarning(error);
            throw new Cod.ApplicationException(InternalError.BadRequest, error) { Reference = error };
        }

        List<Listing> listings = [];
        foreach (var item in request.Cart)
        {
            var listing = await listingRepo.RetrieveAsync(Listing.BuildPartitionKey(item.Listing), Listing.BuildRowKey(item.Option), cancellationToken: cancellationToken);
            if (listing is null)
            {
                var error = $"Invalid listing: {item.Listing} with option: {item.Option}";
                logger.LogWarning(error);
                throw new Cod.ApplicationException(InternalError.NotFound, error) { Reference = error };
            }

            for (int i = 0; i < item.Quantity; i++)
            {
                listings.Add(listing);
            }
        }

        var currencies = listings.Select(x => x.Currency).Distinct().ToList();
        if (currencies.Count > 1 || currencies.Single() != shippingOption.Currency)
        {
            var error = $"Currency must match: {string.Join(',', currencies)}";
            logger.LogWarning(error);
            throw new Cod.ApplicationException(InternalError.BadRequest, error) { Reference = error };
        }

        if (!Currency.TryParse(currencies.Single(), out _))
        {
            var error = $"Invalid currency: {currencies.Single()}";
            logger.LogWarning(error);
            throw new Cod.ApplicationException(InternalError.BadRequest, error) { Reference = error };
        }

        double taxRate;
        string taxKind;
        string culture;
        if (country == Country.NewZealand)
        {
            taxRate = 0.15;
            taxKind = "GST";
            culture = "en-NZ";
        }
        else if (country == Country.Australia)
        {
            taxRate = 0.1;
            taxKind = "GST";
            culture = "en-AU";
        }
        else
        {
            var error = $"Unsupported country for tax calculation: {country}";
            logger.LogWarning(error);
            throw new Cod.ApplicationException(InternalError.BadRequest, error) { Reference = error };
        }

        request.Customer = request.ID;
        request.Created = DateTimeOffset.UtcNow;
        request.Status = (int)OrderStatus.Created;
        request.Discount = 0;
        request.Coupon = null;
        request.Items = string.Join(Order.GetItemsSplitor(), [.. listings.Select(l => l.GetFullID())]);
        request.SubTotal = listings.Sum(x => x.Price);
        request.ShippingCost = shippingOption.Price;
        request.TaxRate = (long)(taxRate * 10000);
        request.TaxKind = taxKind;
        var taxableAmount = request.SubTotal + request.ShippingCost - request.Discount;
        request.Tax = (long)(taxableAmount * taxRate);
        request.GrandTotal = taxableAmount + request.Tax;
        request.Paid = 0;
        request.Currency = currencies.Single();
        request.Culture = culture;
        request.ShippingStatus = (int)ShippingStatus.Pending;
        request.IP = clientIP;

        var result = await orderRepo.CreateAsync(request, cancellationToken: cancellationToken);
        return new OkObjectResult(result);
    }
}