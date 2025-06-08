using System.Text.Json;
using Cod;
using Cod.Platform;
using Cod.Platform.Captcha.ReCaptcha;
using Cod.Platform.Finance;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
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
        var tenant = ParseTenant(req);
        var request = await JsonSerializer.DeserializeAsync<OrderRequest>(req.Body, options: serializationOptions, cancellationToken: cancellationToken);
        request = this.ValidateRequest(request, out var validationError);
        if (validationError is not null)
        {
            return validationError;
        }

        var clientIP = req.GetRemoteIP();
        var riskTask = this.AccessRiskAsync(req, clientIP, tenant, request, cancellationToken);
        var shippingOption = await this.GetShippingOption(request, cancellationToken);
        var country = this.GetOrderCountry(request, shippingOption);
        var listings = await this.GetListingsAsync(request, cancellationToken);
        var currency = this.GetOrderCurrency(shippingOption, listings);
        var (taxRate, taxKind) = this.GetTaxInfo(listings);
        var newOrder = CreateNewOrder(request);

        newOrder.Items = string.Join(Order.GetItemsSplitor(), [.. listings.Select(l => l.GetFullID())]);
        newOrder.ShippingCost = shippingOption.Price;
        newOrder.TaxKind = taxKind;
        newOrder.TaxRate = taxRate;
        newOrder.IP = clientIP;
        newOrder.Paid = 0;
        newOrder.Currency = currency;
        newOrder.Discount = 0; // Assuming no discount for simplicity, can be modified to apply discounts if needed.
        newOrder.SubTotal = listings.Sum(x => x.Price);
        var taxableAmount = newOrder.SubTotal + newOrder.ShippingCost - newOrder.Discount;
        newOrder.Tax = (long)(taxableAmount * (taxRate / 10000m));
        newOrder.GrandTotal = taxableAmount + newOrder.Tax;

        await riskTask;
        var result = await orderRepo.CreateAsync(newOrder, cancellationToken: cancellationToken);
        return new OkObjectResult(result);
    }

    private static Order CreateNewOrder(OrderRequest request) =>
        new()
        {
            Customer = Guid.NewGuid(),
            Created = DateTimeOffset.UtcNow,
            Status = (int)OrderStatus.Created,
            ShippingStatus = (int)ShippingStatus.Pending,

            BillingAddressLine1 = request.ShippingAddressLine1,
            BillingAddressLine2 = request.ShippingAddressLine2,
            BillingBusiness = request.BillingBusiness,
            BillingCity = request.ShippingCity,
            BillingCountry = request.ShippingCountry,
            BillingName = request.Consignee,
            BillingPostcode = request.ShippingPostcode,
            BillingState = request.ShippingState,
            Consignee = request.Consignee,
            Coupon = request.Coupon,
            Email = request.Email,
            Notes = request.Notes,
            Phone = request.Phone,
            ShippingAddressLine1 = request.ShippingAddressLine1,
            ShippingAddressLine2 = request.ShippingAddressLine2,
            ShippingCity = request.ShippingCity,
            ShippingCountry = request.ShippingCountry,
            ShippingPostcode = request.ShippingPostcode,
            ShippingState = request.ShippingState,
            ShippingSuburb = request.ShippingSuburb,
            TimeZone = request.TimeZone,
            Culture = request.Culture,
        };

    private (long taxRate, string? taxKind) GetTaxInfo(List<Listing> listings)
    {
        var taxKinds = listings.Where(x => x.TaxKind != null).Select(x => x.TaxKind).Distinct().ToList();
        if (taxKinds.Count > 1)
        {
            var error = $"Tax does not match across the same order: {string.Join(',', listings.Select(x => x.ID))}";
            logger.LogWarning(error);
            throw new Cod.ApplicationException(InternalError.BadRequest, error) { Reference = error };
        }

        var taxRates = listings.Select(x => x.TaxRate).Distinct().ToList();
        if (taxRates.Count > 1)
        {
            var error = $"Tax rate does not match across the same order: {string.Join(',', listings.Select(x => x.ID))}";
            logger.LogWarning(error);
            throw new Cod.ApplicationException(InternalError.BadRequest, error) { Reference = error };
        }

        return (taxRates.Single(), taxKinds.Single());
    }

    private Currency GetOrderCurrency(ShippingOption shippingOption, List<Listing> listings)
    {
        var currencies = listings.Select(x => x.Currency).Distinct().ToList();
        if (currencies.Count > 1 || currencies.Single() != shippingOption.Currency)
        {
            var error = $"Currency must match: {string.Join(',', currencies)}";
            logger.LogWarning(error);
            throw new Cod.ApplicationException(InternalError.BadRequest, error) { Reference = error };
        }

        if (!Currency.TryParse(currencies.Single(), out var currency))
        {
            var error = $"Invalid currency: {currencies.Single()}";
            logger.LogWarning(error);
            throw new Cod.ApplicationException(InternalError.BadRequest, error) { Reference = error };
        }

        return currency;
    }

    private async Task<List<Listing>> GetListingsAsync(OrderRequest request, CancellationToken cancellationToken)
    {
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

        return listings;
    }

    private Country GetOrderCountry(OrderRequest request, ShippingOption shippingOption)
    {
        var country = Country.Parse(request.ShippingCountry);

        var isShippingOptionSupported = false;
        var supportedCountries = shippingOption.GetCountries();
        foreach (var supportedCountry in supportedCountries)
        {
            if (!Country.TryParse(supportedCountry, out var c))
            {
                logger.LogWarning($"Invalid country code on shipping option {shippingOption.ID}: {supportedCountry}");
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

        return country;
    }

    private async Task<ShippingOption> GetShippingOption(OrderRequest request, CancellationToken cancellationToken)
    {
        var shippingOption = await shippingRepo.RetrieveAsync(ShippingOption.BuildPartitionKey(), ShippingOption.BuildRowKey(request.Shipping), cancellationToken: cancellationToken);
        if (shippingOption is null)
        {
            var error = $"Invalid shipping option: {request.Shipping}";
            logger.LogWarning(error);
            throw new Cod.ApplicationException(InternalError.BadRequest, error) { Reference = error };
        }

        return shippingOption;
    }

    private async Task AccessRiskAsync(HttpRequest req, string clientIP, string tenant, OrderRequest request, CancellationToken cancellationToken)
    {
        var lowRisk = await assessor.AssessAsync(request.ID, tenant, request.Captcha, clientIP, cancellationToken);
        if (!lowRisk)
        {
            var error = $"{clientIP} is considered high risk for order: {request.ID}";
            logger.LogWarning(error);
            throw new Cod.ApplicationException(InternalError.Forbidden, error);
        }
    }

    private OrderRequest ValidateRequest(OrderRequest? request, out IActionResult? validationError)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.TryValidate(out var validationState);

        if (!Country.TryParse(request.ShippingCountry, out _))
        {
            validationState.AddError(nameof(request.ShippingCountry), $"Invalid country code: {request.ShippingCountry}");
        }

        if (request.Cart.Count == 0)
        {
            var error = $"No valid listings found from the order: {request.ID}";
            logger.LogWarning(error);
            validationState.AddError(nameof(request.Cart), error);
        }

        if (!validationState.IsValid)
        {
            logger.LogWarning("Validation failed for order request: {Errors}", JsonSerializer.Serialize(validationState.ToDictionary(), serializationOptions));
            validationError = validationState.MakeResponse();
        }
        else
        {
            validationError = null;
        }

        return request;
    }

    private static string ParseTenant(HttpRequest req)
    {
        var referer = req.Headers.Referer.SingleOrDefault();
        Uri? refererUri = null;
        if (referer == null || !Uri.TryCreate(referer, UriKind.Absolute, out refererUri))
        {
#if !DEBUG
            return new BadRequestResult();
#endif
        }

        var tenant = refererUri?.Host.ToLowerInvariant();
        return tenant!;
    }
}