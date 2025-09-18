using System.Reflection;
using Azure;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Niobium.Finance;
using Niobium.Platform.Finance;
using Niobium.Store.Domains;
using Niobium.Store.Flows;
using Niobium.Store.Options;

namespace Niobium.Store.Core.Tests.Flows
{
    // Purpose
    // Business-focused tests for OrderFlow using a real OrderDomain and mocking only external boundaries
    // (QuoteFlow, IPaymentService, loggers, and repository). Tests are written in a Scenario/Given/When/Then style.
    [TestClass]
    public class OrderFlowTests
    {
        // Scenario: Customer places an order successfully up to charging stage, but payment provider fails to return instruction
        // Given: A valid order request and a successful quote
        // When: Payment returns no instruction (failure)
        // Then: Order is created with business defaults and a payment is attempted with correct details, but flow throws an error for the missing instruction
        [TestMethod]
        public async Task Payment_failure_logs_and_throws_and_charge_request_is_correct()
        {
            var tenant = Guid.NewGuid();
            var clientIP = "203.0.113.9";
            var request = BuildOrderRequest(tenant);
            request.Cart = [new CartItem { Listing = 1, Option = "Default", Quantity = 2 }];

            // Prepare quote inputs via real QuoteFlow (domains in scope)
            var tax = new Tax(1000, TaxKind.GST);
            var listing = BuildListing(1, "Default", 500, "USD", tax.Rate, (int)tax.Kind);
            var shipping = BuildShippingOption(10, 900, "USD", ["US"]);
            var listingRepo = new Mock<IDomainRepository<ListingDomain, Listing>>(MockBehavior.Strict);
            _ = listingRepo.Setup(r => r.GetAsync(Listing.BuildPartitionKey(1), Listing.BuildRowKey("Default"), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(BuildInitializedListingDomain(listing));
            var shippingRepo = new Mock<IDomainRepository<ShippingOptionDomain, ShippingOption>>(MockBehavior.Strict);
            _ = shippingRepo.Setup(r => r.GetAsync(ShippingOption.BuildPartitionKey(), ShippingOption.BuildRowKey(10), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(BuildInitializedShippingDomain(shipping));
            var promoRepo = new Mock<IDomainRepository<PromotionDomain, Promotion>>(MockBehavior.Strict);
            var quoteFlow = new QuoteFlow(shippingRepo.Object, listingRepo.Object, promoRepo.Object, new Mock<ILogger<QuoteFlow>>().Object);

            var storedOrder = default(Order);
            var orderDomainFactory = BuildOrderDomainFactory(o => storedOrder = o, conflictOnCreate: false);

            var payment = new Mock<IPaymentService>(MockBehavior.Strict);
            ChargeRequest? capturedCharge = null;
            _ = payment
                .Setup(p => p.ChargeAsync(It.IsAny<ChargeRequest>()))
                .Callback<ChargeRequest>(cr => capturedCharge = cr)
                .ReturnsAsync((OperationResult<ChargeResponse>)null!); // force failure path

            var flow = new OrderFlow(orderDomainFactory, new Lazy<IPaymentService>(() => payment.Object), quoteFlow, new Mock<ILogger<OrderFlow>>().Object);

            Func<Task> act = async () => await flow.RunAsync(request, clientIP, CancellationToken.None);
            var ex = await act.Should().ThrowAsync<ApplicationException>();
            _ = ex.Which.ErrorCode.Should().Be(InternalError.InternalServerError);

            // Order persisted with expected business defaults
            _ = storedOrder.Should().NotBeNull();
            _ = storedOrder!.Status.Should().Be((int)OrderStatus.Created);
            _ = storedOrder.ShippingStatus.Should().Be((int)ShippingStatus.NotApplicable);
            _ = storedOrder.Settled.Should().Be(0);
            _ = storedOrder.Currency.Should().Be("USD");
            _ = storedOrder.SubTotal.Should().Be(2 * 500);
            _ = storedOrder.ShippingCost.Should().Be(900);
            _ = storedOrder.GrandTotal.Should().Be(storedOrder.SubTotal + storedOrder.ShippingCost - storedOrder.Discount + storedOrder.Tax);

            // Charge request reflects the just-created order and quote
            _ = capturedCharge.Should().NotBeNull();
            _ = capturedCharge!.TargetKind.Should().Be(ChargeTargetKind.User);
            _ = capturedCharge.Target.Should().Be(request.ID.ToString());
            _ = capturedCharge.Tenant.Should().Be(tenant.ToString());
            _ = capturedCharge.Amount.Should().Be(storedOrder.GrandTotal);
            _ = capturedCharge.Currency.ToString().Should().Be("USD");
            _ = capturedCharge.IP.Should().Be(clientIP);
        }

        // Scenario: Quote validation failed (e.g., shipping unsupported or currency mismatch)
        // Given: QuoteFlow rejects with BadRequest
        // When: OrderFlow runs
        // Then: The same error is surfaced and payment is not attempted
        [TestMethod]
        public async Task Quote_failure_is_surfaced_and_payment_not_attempted()
        {
            var tenant = Guid.NewGuid();
            var request = BuildOrderRequest(tenant);
            request.Cart = [new CartItem { Listing = 1, Option = "Default", Quantity = 1 }];

            // Build QuoteFlow that will fail due to unsupported country
            var tax = new Tax(1000, TaxKind.GST);
            var listing = BuildListing(1, "Default", 500, "USD", tax.Rate, (int)tax.Kind);
            var shipping = BuildShippingOption(10, 900, "USD", new[] { "FR" }); // US not supported -> fail
            var listingRepo = new Mock<IDomainRepository<ListingDomain, Listing>>(MockBehavior.Strict);
            _ = listingRepo.Setup(r => r.GetAsync(Listing.BuildPartitionKey(1), Listing.BuildRowKey("Default"), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(BuildInitializedListingDomain(listing));
            var shippingRepo = new Mock<IDomainRepository<ShippingOptionDomain, ShippingOption>>(MockBehavior.Strict);
            _ = shippingRepo.Setup(r => r.GetAsync(ShippingOption.BuildPartitionKey(), ShippingOption.BuildRowKey(10), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(BuildInitializedShippingDomain(shipping));
            var promoRepo = new Mock<IDomainRepository<PromotionDomain, Promotion>>(MockBehavior.Strict);
            var quoteFlow = new QuoteFlow(shippingRepo.Object, listingRepo.Object, promoRepo.Object, new Mock<ILogger<QuoteFlow>>().Object);

            var orderDomainFactory = BuildOrderDomainFactory(_ => { }, conflictOnCreate: false);
            var payment = new Mock<IPaymentService>(MockBehavior.Strict);

            var flow = new OrderFlow(orderDomainFactory, new Lazy<IPaymentService>(() => payment.Object), quoteFlow, new Mock<ILogger<OrderFlow>>().Object);

            Func<Task> act = async () => await flow.RunAsync(request, "198.51.100.3", CancellationToken.None);
            var ex = await act.Should().ThrowAsync<ApplicationException>();
            _ = ex.Which.ErrorCode.Should().Be(InternalError.BadRequest);

            payment.Verify(p => p.ChargeAsync(It.IsAny<ChargeRequest>()), Times.Never);
        }

        // Scenario: User submits order twice quickly (duplicate); first save conflicts but flow proceeds idempotently
        // Given: Repository create triggers a 409, simulating duplicate create
        // When: OrderFlow runs
        // Then: It still proceeds to attempt payment (idempotent), without crashing
        [TestMethod]
        public async Task Duplicate_submission_does_not_crash_and_payment_still_attempted()
        {
            var tenant = Guid.NewGuid();
            var request = BuildOrderRequest(tenant);
            request.Cart = [new CartItem { Listing = 9, Option = "Default", Quantity = 1 }];

            var tax = new Tax(1500, TaxKind.GST);
            var listing = BuildListing(9, "Default", 800, "USD", tax.Rate, (int)tax.Kind);
            var shipping = BuildShippingOption(10, 900, "USD", new[] { "US" });
            var listingRepo = new Mock<IDomainRepository<ListingDomain, Listing>>(MockBehavior.Strict);
            _ = listingRepo.Setup(r => r.GetAsync(Listing.BuildPartitionKey(9), Listing.BuildRowKey("Default"), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(BuildInitializedListingDomain(listing));
            var shippingRepo = new Mock<IDomainRepository<ShippingOptionDomain, ShippingOption>>(MockBehavior.Strict);
            _ = shippingRepo.Setup(r => r.GetAsync(ShippingOption.BuildPartitionKey(), ShippingOption.BuildRowKey(10), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(BuildInitializedShippingDomain(shipping));
            var promoRepo = new Mock<IDomainRepository<PromotionDomain, Promotion>>(MockBehavior.Strict);
            var quoteFlow = new QuoteFlow(shippingRepo.Object, listingRepo.Object, promoRepo.Object, new Mock<ILogger<QuoteFlow>>().Object);

            var orderDomainFactory = BuildOrderDomainFactory(_ => { }, conflictOnCreate: true);

            var payment = new Mock<IPaymentService>(MockBehavior.Strict);
            _ = payment.Setup(p => p.ChargeAsync(It.IsAny<ChargeRequest>()))
                .ReturnsAsync((OperationResult<ChargeResponse>)null!);

            var flow = new OrderFlow(orderDomainFactory, new Lazy<IPaymentService>(() => payment.Object), quoteFlow, new Mock<ILogger<OrderFlow>>().Object);

            Func<Task> act = async () => await flow.RunAsync(request, "198.51.100.7", CancellationToken.None);
            _ = await act.Should().ThrowAsync<ApplicationException>();

            payment.Verify(p => p.ChargeAsync(It.IsAny<ChargeRequest>()), Times.Once);
        }

        // Helpers
        private static Func<OrderDomain> BuildOrderDomainFactory(Action<Order> onCreateSaved, bool conflictOnCreate)
        {
            var repo = new Mock<IRepository<Order>>(MockBehavior.Strict);

            _ = conflictOnCreate
                ? repo.Setup(r => r.CreateAsync(It.IsAny<IEnumerable<Order>>(), It.IsAny<bool>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new RequestFailedException(409, "conflict"))
                : repo.Setup(r => r.CreateAsync(It.IsAny<IEnumerable<Order>>(), It.IsAny<bool>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((IEnumerable<Order> orders, bool _, DateTimeOffset? __, CancellationToken ___) => { var o = orders.Single(); onCreateSaved(o); return orders; });

            var handlers = Array.Empty<IDomainEventHandler<IDomain<Order>>>();
            var options = Microsoft.Extensions.Options.Options.Create(new StoreOptions { InvoicingTenant = Guid.NewGuid() });
            var logger = new Mock<ILogger<OrderDomain>>(MockBehavior.Loose);

            return () => new OrderDomain(new Lazy<IRepository<Order>>(() => repo.Object), handlers, options, logger.Object);
        }

        private static Listing BuildListing(int id, string option, long price, string currency, long taxRate, int taxKind)
            => new()
            {
                ID = id,
                Option = option,
                Name = $"Item {id}",
                Price = price,
                Currency = currency,
                SKU = $"SKU-{id}",
                TaxRate = taxRate,
                TaxKind = taxKind,
                ShippingOptions = "10",
                Culture = "en-US",
            };

        private static ShippingOption BuildShippingOption(int id, long price, string currency, string[] countries)
            => new()
            {
                PartitionKey = ShippingOption.BuildPartitionKey(),
                ID = id,
                Name = id.ToString(),
                Price = price,
                Currency = currency,
                Countries = String.Join(',', countries),
            };

        private static ListingDomain BuildInitializedListingDomain(Listing listing)
        {
            var repo = new Mock<IRepository<Listing>>(MockBehavior.Loose);
            var handlers = Array.Empty<IDomainEventHandler<IDomain<Listing>>>();
            var domain = new ListingDomain(new Lazy<IRepository<Listing>>(() => repo.Object), handlers);
            InitializeDomain(domain, listing);
            return domain;
        }

        private static ShippingOptionDomain BuildInitializedShippingDomain(ShippingOption option)
        {
            var repo = new Mock<IRepository<ShippingOption>>(MockBehavior.Loose);
            var handlers = Array.Empty<IDomainEventHandler<IDomain<ShippingOption>>>();
            var logger = new Mock<ILogger<ShippingOptionDomain>>(MockBehavior.Loose);
            var domain = new ShippingOptionDomain(new Lazy<IRepository<ShippingOption>>(() => repo.Object), handlers, logger.Object);
            InitializeDomain(domain, option);
            return domain;
        }

        private static void InitializeDomain<TDomain, TEntity>(TDomain domain, TEntity entity)
        {
            var method = GetMethodRecursive(domain!.GetType(), "Initialize", new[] { typeof(TEntity) })
                ?? throw new InvalidOperationException($"Could not find Initialize({typeof(TEntity).Name}) on {domain.GetType().Name}");
            _ = method.Invoke(domain, new object[] { entity! });
        }

        private static MethodInfo? GetMethodRecursive(Type type, string name, Type[] parameterTypes)
        {
            while (type != null)
            {
                var mi = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, binder: null, types: parameterTypes, modifiers: null);
                if (mi != null)
                {
                    return mi;
                }

                type = type.BaseType!;
            }
            return null;
        }

        private static OrderRequest BuildOrderRequest(Guid tenant)
            => new()
            {
                ID = Guid.NewGuid(),
                Tenant = tenant,
                Shipping = 10,
                ShippingCountry = "US",
                Captcha = "dummy",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Culture = "en-US",
                TimeZone = "UTC",
                Consignee = "John Doe",
                Email = "john@example.com",
                ShippingAddressLine1 = "123 Main St",
                ShippingCity = "City",
                ShippingPostcode = "10001",
                ShippingState = null,
                BillingName = "John Doe",
                BillingAddressLine1 = "123 Main St",
                BillingCity = "City",
                BillingCountry = "US",
                BillingPostcode = "10001",
                Cart = [new CartItem { Listing = 1, Option = "Default", Quantity = 1 }],
            };

        private static QuoteResponse BuildQuoteResponse(QuoteRequest request, params (int listing, string option, long unit, int qty, string currency, long taxRate, TaxKind taxKind)[] lines)
        {
            var items = new List<PricedCartItem>();
            foreach (var (listing, option, unit, qty, currency, taxRate, taxKind) in lines)
            {
                var amount = unit * qty;
                items.Add(new PricedCartItem
                {
                    Listing = listing,
                    Option = option,
                    Quantity = qty,
                    Unit = unit,
                    Was = amount,
                    Now = amount,
                    Discount = 0,
                    Tax = new Tax(taxRate, taxKind),
                    Currency = currency,
                });
            }

            var baseline = items.First();
            var shippingQuote = new TaxableAmount
            {
                Amount = new Amount { Cents = 900, Currency = baseline.Currency },
                Tax = baseline.Tax
            };

            var quote = new QuoteResponse(request, items, shippingQuote);
            quote.Update();
            return quote;
        }
    }
}
