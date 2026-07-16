using System.Net;
using Azure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Niobium.Finance;
using Niobium.Invoicing;
using Niobium.Notification;
using Niobium.Store.Options;
using Transaction = Niobium.Finance.Transaction;

namespace Niobium.Store.Domains
{
    public class OrderDomain(
        Lazy<IRepository<Order>> repository,
        IEnumerable<IDomainEventHandler<IDomain<Order>>> eventHandlers,
        IOptions<StoreOptions> options,
        ILogger<OrderDomain> logger)
        : GenericDomain<Order>(repository, eventHandlers)
    {
        private const string OrderConfirmedNotificationChannel = "OrderConfirmed";
        private const string OrderShippedNotificationChannel = "OrderShipped";
        private const string OrderDeliveredNotificationChannel = "OrderDelivered";

        public async Task<Order> TakeNew(OrderRequest request, QuoteResponse quote, string? clientIP, CancellationToken cancellationToken = default)
        {
            var newOrder = CreateNewOrder(request);
            newOrder.ShippingCost = quote.ShippingCost;
            newOrder.Shipping = quote.Shipping;
            newOrder.ShippingDescription = quote.ShippingDescription;
            newOrder.TaxKind = (int)quote.TaxInfo.Kind;
            newOrder.TaxRate = quote.TaxInfo.Rate;
            newOrder.IP = clientIP;
            newOrder.Settled = 0;
            newOrder.Currency = quote.Currency;
            newOrder.Discount = quote.Discount;
            newOrder.Total = quote.Total;
            newOrder.Tax = quote.Tax;
            newOrder.SetCart(request.Cart);

            _ = this.Initialize(newOrder);
            try
            {
                await this.SaveAsync(cancellationToken: cancellationToken);
                return newOrder;
            }
            catch (RequestFailedException e) when (e.Status == (int)HttpStatusCode.Conflict)
            {
                logger.LogWarning($"Order {newOrder.GetFullID()} already exists (detected during creation). Fetching existing record.");
                return await this.GetEntityAsync(cancellationToken);
            }
        }

        public async Task<Amount> FigureDueAsync(CancellationToken cancellationToken)
        {
            var entity = await this.GetEntityAsync(cancellationToken);

            if (entity.Status >= (int)OrderStatus.Paid)
            {
                return Amount.Zero; // Order is already settled or paid, nothing to do.
            }

            if (entity.Status != (int)OrderStatus.Created)
            {
                return Amount.Zero; // Order is not in a state that requires settlement.
            }

            var due = entity.Total - entity.Settled;
            if (due <= 0)
            {
                return Amount.Zero; // No payment due, nothing to settle.
            }

            return new Amount { Cents = due, Currency = entity.Currency };
        }

        public async Task<bool> SettleAsync(Transaction transaction, CancellationToken cancellationToken)
        {
            this.CheckInitialized();
            var entity = await this.GetEntityAsync(cancellationToken);
            entity.Settled += Math.Abs(transaction.Delta);

            if (String.IsNullOrWhiteSpace(entity.Transactions))
            {
                entity.Transactions = transaction.GetID().ToString();
            }
            else
            {
                entity.Transactions += $",{transaction.GetID()}";
            }

            if (entity.Settled >= entity.Total)
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

            await this.SaveAsync(force: true, cancellationToken: cancellationToken);
            var result = entity.Settled >= entity.Total;
            var fullID = new StorageKey(this.PartitionKey, this.RowKey);
            logger.LogInformation($"Order {fullID} settled {result} by transaction: {transaction.GetID()}");
            return result;
        }

        public async Task UpdateTrackingAsync(OrderStatus shippingStatus, CancellationToken cancellationToken)
        {
            var entity = await this.GetEntityAsync(cancellationToken);
            if (entity.Status >= (int)shippingStatus)
            {
                logger.LogWarning($"Order {entity.GetFullID()} status {entity.Status} is already ahead of or equal to {shippingStatus}, no update needed.");
                return;
            }

            entity.Status = (int)shippingStatus;
            await this.SaveAsync(force: true, cancellationToken: cancellationToken);
        }

        public async Task<ChargeRequest> CreateChargeAsync(string? clientIP = null, CancellationToken cancellationToken = default)
        {
            var due = await this.FigureDueAsync(cancellationToken);
            var entity = await this.GetEntityAsync(cancellationToken);
            return new ChargeRequest
            {
                TargetKind = ChargeTargetKind.User,
                Target = entity.Customer.ToString(),
                Channel = PaymentChannels.Cards,
                Operation = PaymentOperationKind.Charge,
                Tenant = entity.Tenant.ToString(),
                Order = entity.GetID().ToString(),
                Amount = due.Cents,
                Currency = due.Currency,
                IP = clientIP,
            };
        }

        public async Task<NotifyCommand?> GenerateNotificationAsync(CancellationToken cancellationToken = default)
        {
            var entity = await this.GetEntityAsync(cancellationToken);
            string? channel = null;
            string? id = null;
            switch ((OrderStatus)entity.Status)
            {
                case OrderStatus.Paid:
                    id = $"{OrderConfirmedNotificationChannel}-{entity.GetFullID()}";
                    channel = OrderConfirmedNotificationChannel;
                    break;
                case OrderStatus.Shipped:
                    id = $"{OrderShippedNotificationChannel}-{entity.GetFullID()}";
                    channel = OrderShippedNotificationChannel;
                    break;
                case OrderStatus.Delivered:
                    id = $"{OrderDeliveredNotificationChannel}-{entity.GetFullID()}";
                    channel = OrderDeliveredNotificationChannel;
                    break;
            }

            if (id == null || channel == null)
            {
                logger.LogWarning($"Order {entity.GetFullID()} status {entity.Status} does not require notification.");
                return null;
            }

            var parameters = entity.BuildNotificationParameters();
            return new NotifyCommand
            {
                ID = id,
                Tenant = entity.Tenant,
                Channel = channel,
                Destination = entity.Email,
                DestinationDisplayName = entity.Consignee,
                Parameters = parameters.ToDictionary(),
            };
        }

        public async Task<IssueInvoiceCommand> IssueInvoiceAsync(CancellationToken cancellationToken = default)
        {
            var entity = await this.GetEntityAsync(cancellationToken);
            var invoice = new IssueInvoiceCommand
            {
                ID = entity.GetFullID(),
                InvoiceID = entity.GetID(),
                Tenant = options.Value.InvoicingTenant,
                BilleeID = entity.Customer,
                BillerID = entity.Tenant,
                BillingPeriodStartDay = DateTimeOffset.UtcNow,
                DueBy = null,
                InvoiceCycle = (int)InvoiceCycle.Once,
                NotifyBillee = true,
                Reference = entity.GetID().ToString(),
                InvoiceItems = [],
                Settled = new Amount
                {
                    Currency = entity.Currency,
                    Cents = entity.Settled
                },
                Billee = new Billee
                {
                    Name = entity.BillingName,
                    Email = entity.Email,
                    AddressLine1 = entity.BillingAddressLine1,
                    AddressLine2 = entity.BillingAddressLine2,
                    Suburb = entity.BillingSuburb,
                    State = entity.BillingState,
                    Country = entity.BillingCountry,
                    Biller = entity.Tenant,
                    City = entity.BillingCity,
                    Culture = entity.Culture,
                    Currency = entity.Currency,
                    ID = entity.Customer,
                    Phone = entity.Phone,
                    TimeZone = entity.TimeZone,
                    Zipcode = entity.BillingPostcode,
                }
            };

            var shippingCostBeforeTax = (long)Math.Round(entity.ShippingCost * 10000 / (10000m + entity.TaxRate), 0, MidpointRounding.AwayFromZero);
            invoice.InvoiceItems.Add(new InvoiceItem
            {
                ID = invoice.InvoiceID,
                Invoice = entity.Created,
                Subject = "Shipping",
                Description = entity.ShippingDescription,
                Quantity = 1,
                UnitPriceCents = shippingCostBeforeTax,
                UnitPriceCurrency = entity.Currency,
                LineTotalCents = shippingCostBeforeTax,
                LineTotalCurrency = entity.Currency
            });

            return invoice;
        }

        private static Order CreateNewOrder(OrderRequest request) =>
            new()
            {
                Customer = request.ID,
                Created = DateTimeOffset.FromUnixTimeMilliseconds(request.Timestamp),
                Status = (int)OrderStatus.Created,
                ShippingStatus = (int)ShippingStatus.NotApplicable,

                BillingAddressLine1 = request.ShippingAddressLine1,
                BillingAddressLine2 = request.ShippingAddressLine2,
                BillingBusiness = request.BillingBusiness,
                BillingSuburb = request.ShippingSuburb,
                BillingCity = request.ShippingCity,
                BillingState = request.ShippingState,
                BillingCountry = request.ShippingCountry,
                BillingPostcode = request.ShippingPostcode,
                BillingName = request.BillingName ?? request.Consignee,
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
                MarketingSubscription = request.MarketingSubscription,
                Track = request.Track,
            };
    }
}
