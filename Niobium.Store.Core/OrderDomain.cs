using Cod;
using Cod.Platform.Finance;
using Microsoft.Extensions.Logging;

namespace Niobium.Store
{
    public class OrderDomain(
        Lazy<IRepository<Order>> repository,
        IEnumerable<IDomainEventHandler<IDomain<Order>>> eventHandlers,
        IDomainRepository<ShippingOptionDomain, ShippingOption> shippingRepo,
        IRepository<Listing> listingRepo,
        ILogger<OrderDomain> logger)
        : GenericDomain<Order>(repository, eventHandlers)
    {
        public async Task<Order> TakeNew(OrderRequest request, string? clientIP, CancellationToken cancellationToken = default)
        {
            var shippingDomain = await this.GetShippingOption(request, cancellationToken);
            var country = await shippingDomain.FigureCountryAsync(request.ShippingCountry, cancellationToken);
            var listings = await this.GetListingsAsync(request, cancellationToken);
            var shippingEntity = await shippingDomain.GetEntityAsync(cancellationToken);
            var currency = this.FigureCurrency(shippingEntity, listings);
            var (taxRate, taxKind) = this.FigureTaxInfo(listings);

            var newOrder = CreateNewOrder(request);
            newOrder.Items = string.Join(Order.GetItemsSplitor(), [.. listings.Select(l => l.GetFullID())]);
            newOrder.ShippingCost = shippingEntity.Price;
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

            this.Initialize(newOrder);
            await this.SaveAsync(cancellationToken: cancellationToken);
            await this.OnEvent(new OrderUpdatedEvent { Order = newOrder }, cancellationToken);
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
                if (entity.Status == (int)OrderStatus.Created)
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
            await this.OnEvent(new OrderUpdatedEvent { Order = entity }, cancellationToken);
            return entity.Paid >= entity.GrandTotal;
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

        private async Task<ShippingOptionDomain> GetShippingOption(OrderRequest request, CancellationToken cancellationToken)
        {
            var shippingOption = await shippingRepo.GetAsync(ShippingOption.BuildPartitionKey(), ShippingOption.BuildRowKey(request.Shipping), cancellationToken: cancellationToken);
            if (shippingOption is null)
            {
                var error = $"Invalid shipping option: {request.Shipping}";
                logger.LogWarning(error);
                throw new Cod.ApplicationException(InternalError.BadRequest, error) { Reference = error };
            }

            return shippingOption;
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
            };
    }
}
