using Cod;
using Cod.Finance;
using Cod.Platform.Finance;
using Microsoft.Extensions.Logging;

namespace Niobium.Store
{
    public class OrderDomain(
        Lazy<IRepository<Order>> repository,
        IEnumerable<IDomainEventHandler<IDomain<Order>>> eventHandlers,
        IDomainRepository<ShippingOptionDomain, ShippingOption> shippingRepo,
        IRepository<Listing> listingRepo,
        Lazy<IPaymentService> paymentService,
        ILogger<OrderDomain> logger)
        : GenericDomain<Order>(repository, eventHandlers)
    {
        public async Task<QuoteResponse> QuoteAsync(QuoteRequest request, CancellationToken cancellationToken = default)
        {
            var shippingDomain = await this.GetShippingOption(request.Shipping, cancellationToken);
            var country = await shippingDomain.FigureCountryAsync(request.ShippingCountry, cancellationToken);
            var listings = await this.GetListingsAsync(request.Cart, cancellationToken);
            var shippingEntity = await shippingDomain.GetEntityAsync(cancellationToken);
            var currency = this.FigureCurrency(shippingEntity, listings);
            var (taxRate, taxKind) = this.FigureTaxInfo(listings);
            var quote = new QuoteResponse(request)
            {
                Discount = new Amount { Cents = 0, Currency = currency }, // Assuming no discount for simplicity, can be modified to apply discounts if needed.
                SubTotal = new Amount { Cents = listings.Sum(x => x.Price), Currency = currency },
                ShippingCost = new Amount { Cents = shippingEntity.Price, Currency = currency },
                TaxRate = taxRate,
                TaxKind = taxKind,
            };
            long taxableAmount = quote.SubTotal.Cents + quote.ShippingCost.Cents - quote.Discount.Cents;
            quote.Tax = new Amount { Cents = taxableAmount * taxRate / 10000, Currency = currency };
            quote.GrandTotal = new Amount { Cents = taxableAmount + quote.Tax.Cents, Currency = currency };
            return quote;
        }

        public async Task<Order> TakeNew(OrderRequest request, string? clientIP, CancellationToken cancellationToken = default)
        {
            var quote = await this.QuoteAsync(request, cancellationToken);
            var newOrder = CreateNewOrder(request);
            newOrder.ShippingCost = quote.ShippingCost.Cents;
            newOrder.TaxKind = quote.TaxKind;
            newOrder.TaxRate = quote.TaxRate;
            newOrder.IP = clientIP;
            newOrder.Paid = 0;
            newOrder.Currency = quote.GrandTotal.Currency;
            newOrder.Discount = quote.Discount.Cents;
            newOrder.SubTotal = quote.SubTotal.Cents;
            newOrder.Tax = quote.Tax.Cents;
            newOrder.GrandTotal = quote.GrandTotal.Cents;

            var listingIDs = request.Cart
                .Select(x => new StorageKey { PartitionKey = Listing.BuildPartitionKey(x.Listing), RowKey = Listing.BuildRowKey(x.Option) })
                .Select(x => x.BuildFullID())
                .ToList();
            newOrder.Items = string.Join(Order.GetItemsSplitor(), listingIDs);

            this.Initialize(newOrder);
            await this.SaveAsync(cancellationToken: cancellationToken);
            return newOrder;
        }

        public async Task<Amount> FigureDueAsync(CancellationToken cancellationToken)
        {
            var entity = await this.GetEntityAsync(cancellationToken);
            if (entity.Status != (int)OrderStatus.Created)
            {
                return Amount.Zero; // Order is not in a state that requires settlement.
            }

            var due = entity.GrandTotal - entity.Paid;
            if (due <= 0)
            {
                return Amount.Zero; // No payment due, nothing to settle.
            }

            return new Amount { Cents = due, Currency = entity.Currency };
        }

        public async Task<bool> PayAsync(Transaction transaction, CancellationToken cancellationToken)
        {
            var entity = await this.GetEntityAsync(cancellationToken);
            entity.Paid += Math.Abs(transaction.Delta);

            if (string.IsNullOrWhiteSpace(entity.Transactions))
            {
                entity.Transactions = transaction.GetID().ToString();
            }
            else
            {
                entity.Transactions += $",{transaction.GetID()}";
            }

            if (entity.Paid >= entity.GrandTotal)
            {
                if (entity.Status < (int)OrderStatus.Paid)
                {
                    entity.Status = (int)OrderStatus.Paid;
                }

                if (entity.ShippingStatus == (int)ShippingStatus.NotApplicable)
                {
                    entity.ShippingStatus = (int)ShippingStatus.Pending;
                }
            }
            else
            {
                if (entity.Status == (int)OrderStatus.Created)
                {
                    entity.Status = (int)OrderStatus.PartiallyPaid;
                }
            }

            await this.SaveAsync(cancellationToken: cancellationToken);
            return entity.Paid >= entity.GrandTotal;
        }

        public async Task<ChargeResponse?> CreateChargeAsync(string? source = null, string? clientIP = null, CancellationToken cancellationToken = default)
        {
            var entity = await this.GetEntityAsync(cancellationToken);
            var chargeRequest = new ChargeRequest
            {
                TargetKind = ChargeTargetKind.User,
                Target = entity.Customer.ToString(),
                Channel = PaymentChannels.Cards,
                Operation = PaymentOperationKind.Charge,
                Source = source,
                Order = entity.GetID().ToString(),
                Amount = entity.GrandTotal,
                Currency = entity.Currency,
                IP = clientIP,
            };

            var charge = await paymentService.Value.ChargeAsync(chargeRequest);
            if (charge == null || !charge.IsSuccess)
            {
                logger.LogError($"Failed to process charge for order {entity.GetFullID()}: {charge?.Message}");
                return null;
            }

            if (charge.Result?.Instruction == null)
            {
                logger.LogError($"Failed to get payment instruction for order {entity.GetFullID()}: {charge.Message}");
                return null;
            }

            return charge.Result;
        }

        private (long taxRate, string? taxKind) FigureTaxInfo(List<Listing> listings)
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

        private Currency FigureCurrency(ShippingOption shippingOption, List<Listing> listings)
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

        private async Task<ShippingOptionDomain> GetShippingOption(int shippingID, CancellationToken cancellationToken)
        {
            var shippingOption = await shippingRepo.GetAsync(ShippingOption.BuildPartitionKey(), ShippingOption.BuildRowKey(shippingID), cancellationToken: cancellationToken);
            if (shippingOption is null)
            {
                var error = $"Invalid shipping option: {shippingID}";
                logger.LogWarning(error);
                throw new Cod.ApplicationException(InternalError.BadRequest, error) { Reference = error };
            }

            return shippingOption;
        }

        private async Task<List<Listing>> GetListingsAsync(List<CartItem> cart, CancellationToken cancellationToken)
        {
            List<Listing> listings = [];
            foreach (var item in cart)
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

        private static Order CreateNewOrder(OrderRequest request) =>
            new()
            {
                Customer = request.ID,
                Created = DateTimeOffsetExtensions.FromReverseUnixTimeMilliseconds(request.Timestamp),
                Status = (int)OrderStatus.Created,
                ShippingStatus = (int)ShippingStatus.NotApplicable,

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
                Tenant = request.Tenant,
            };
    }
}
