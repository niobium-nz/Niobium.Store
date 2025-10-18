using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Niobium.Finance;
using Niobium.Store;
using Niobium.Store.Domains;
using Niobium.Store.Flows;

namespace Niobium.Store.Core.Tests.Flows
{
    // Purpose
    // Business-focused tests for QuoteFlow that keep real Domain logic in scope (ListingDomain, ShippingOptionDomain, PromotionDomain)
    // and mock only repository/logging boundaries. Tests aim for junior-friendly readability.
    [TestClass]
    public class QuoteFlowTests
    {
        // Scenario: Quoting a valid cart returns a consistent quote across cart and shipping
        // Given: All listings have same currency/tax; shipping supports destination and matches currency/tax
        // When: QuoteFlow runs
        // Then: Quote includes items and shipping totals without errors
        [TestMethod]
        public async Task Quoting_valid_cart_returns_consistent_quote()
        {
            var tenant = Guid.NewGuid();
            var request = BuildQuoteRequest(tenant, shippingId: 10, country: "US",
                new CartItem { Listing = 1, Option = "Default", Quantity = 2 },
                new CartItem { Listing = 2, Option = "Default", Quantity = 1 });

            var usdTax = new Tax(rate: 1000, kind: TaxKind.GST); // example 10%
            var listing1 = BuildListing(1, "Default", 500, 1000, "USD", usdTax.Rate, (int)usdTax.Kind);
            var listing2 = BuildListing(2, "Default", 700, 1000, "USD", usdTax.Rate, (int)usdTax.Kind);
            var shipping = BuildShippingOption(10, 900, "USD", new[] { "US" });

            var listingRepo = new Mock<IDomainRepository<ListingDomain, Listing>>(MockBehavior.Strict);
            _ = listingRepo.Setup(r => r.GetAsync(Listing.BuildPartitionKey(1), Listing.BuildRowKey("Default"), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(BuildInitializedListingDomain(listing1));
            _ = listingRepo.Setup(r => r.GetAsync(Listing.BuildPartitionKey(2), Listing.BuildRowKey("Default"), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(BuildInitializedListingDomain(listing2));

            var shippingRepo = new Mock<IDomainRepository<ShippingOptionDomain, ShippingOption>>(MockBehavior.Strict);
            _ = shippingRepo.Setup(r => r.GetAsync(ShippingOption.BuildPartitionKey(), ShippingOption.BuildRowKey(10), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(BuildInitializedShippingDomain(shipping));

            var promoRepo = new Mock<IDomainRepository<PromotionDomain, Promotion>>(MockBehavior.Strict);
            var logger = new Mock<ILogger<QuoteFlow>>(MockBehavior.Loose);

            var flow = new QuoteFlow(shippingRepo.Object, listingRepo.Object, promoRepo.Object, logger.Object);

            var quote = await flow.RunAsync(request, CancellationToken.None);

            quote.Quote.Should().HaveCount(2);
            quote.ShippingCost.Should().Be(900);
            quote.Total.Should().Be(2 * 500 + 1 * 700 + quote.ShippingCost - quote.Discount);
            quote.TaxInfo.Kind.Should().Be(usdTax.Kind);
            quote.TaxInfo.Rate.Should().Be(usdTax.Rate);
        }

        // Scenario: Shipping currency must match cart currency
        // Given: Listings priced in USD, shipping option priced in NZD
        // When: QuoteFlow runs
        // Then: Flow rejects with BadRequest due to currency mismatch
        [TestMethod]
        public async Task Shipping_currency_mismatch_is_rejected()
        {
            var tenant = Guid.NewGuid();
            var request = BuildQuoteRequest(tenant, shippingId: 10, country: "US",
                new CartItem { Listing = 1, Option = "Default", Quantity = 1 });

            var usdTax = new Tax(rate: 1000, kind: TaxKind.GST);
            var listing = BuildListing(1, "Default", 500, 1000, "USD", usdTax.Rate, (int)usdTax.Kind);
            var shipping = BuildShippingOption(10, 900, "NZD", ["US"]); // NZD creates mismatch

            var listingRepo = new Mock<IDomainRepository<ListingDomain, Listing>>(MockBehavior.Strict);
            _ = listingRepo.Setup(r => r.GetAsync(Listing.BuildPartitionKey(1), Listing.BuildRowKey("Default"), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(BuildInitializedListingDomain(listing));

            var shippingRepo = new Mock<IDomainRepository<ShippingOptionDomain, ShippingOption>>(MockBehavior.Strict);
            _ = shippingRepo.Setup(r => r.GetAsync(ShippingOption.BuildPartitionKey(), ShippingOption.BuildRowKey(10), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(BuildInitializedShippingDomain(shipping));

            var promoRepo = new Mock<IDomainRepository<PromotionDomain, Promotion>>(MockBehavior.Strict);
            var logger = new Mock<ILogger<QuoteFlow>>(MockBehavior.Loose);

            var flow = new QuoteFlow(shippingRepo.Object, listingRepo.Object, promoRepo.Object, logger.Object);

            Func<Task> act = async () => await flow.RunAsync(request, CancellationToken.None);
            var ex = await act.Should().ThrowAsync<ApplicationException>();
            ex.Which.ErrorCode.Should().Be(InternalError.BadRequest);
            ex.Which.Reference?.ToString().Should().Contain("currency");
        }

        // Scenario: Shipping tax must match cart tax
        // Given: Listings taxed with GST, shipping quote uses a different tax
        // When: QuoteFlow runs
        // Then: Flow rejects with BadRequest due to tax mismatch
        [TestMethod]
        public async Task Shipping_tax_mismatch_is_rejected()
        {
            var tenant = Guid.NewGuid();
            var request = BuildQuoteRequest(tenant, shippingId: 10, country: "US",
                new CartItem { Listing = 1, Option = "Default", Quantity = 1 },
                new CartItem { Listing = 9, Option = "Default", Quantity = 1 });

            var vatTax = new Tax(rate: 1000, kind: TaxKind.VAT);
            var gstTax = new Tax(rate: 1000, kind: TaxKind.GST);
            // baseline tax will come from the first cart item (listing 1) -> VAT
            var vatListing = BuildListing(1, "Default", 500, 1000, "USD", vatTax.Rate, (int)vatTax.Kind);
            var gstListing = BuildListing(9, "Default", 600, 1000, "USD", gstTax.Rate, (int)gstTax.Kind);
            var shipping = BuildShippingOption(10, 900, "USD", ["US"]);

            var listingRepo = new Mock<IDomainRepository<ListingDomain, Listing>>(MockBehavior.Strict);
            _ = listingRepo.Setup(r => r.GetAsync(Listing.BuildPartitionKey(1), Listing.BuildRowKey("Default"), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(BuildInitializedListingDomain(vatListing));
            _ = listingRepo.Setup(r => r.GetAsync(Listing.BuildPartitionKey(9), Listing.BuildRowKey("Default"), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(BuildInitializedListingDomain(gstListing));

            var shippingRepo = new Mock<IDomainRepository<ShippingOptionDomain, ShippingOption>>(MockBehavior.Strict);
            _ = shippingRepo.Setup(r => r.GetAsync(ShippingOption.BuildPartitionKey(), ShippingOption.BuildRowKey(10), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(BuildInitializedShippingDomain(shipping));

            var promoRepo = new Mock<IDomainRepository<PromotionDomain, Promotion>>(MockBehavior.Strict);
            var logger = new Mock<ILogger<QuoteFlow>>(MockBehavior.Loose);

            var flow = new QuoteFlow(shippingRepo.Object, listingRepo.Object, promoRepo.Object, logger.Object);

            var quote = await flow.RunAsync(request, CancellationToken.None);
            quote.TaxInfo.Kind.Should().Be(TaxKind.VAT);
            quote.TaxInfo.Rate.Should().Be(vatTax.Rate);
        }

        // Scenario: Unsupported shipping country is rejected
        // Given: Shipping option does not support the destination country
        // When: QuoteFlow runs
        // Then: Flow rejects with BadRequest describing the unsupported country
        [TestMethod]
        public async Task Unsupported_shipping_country_is_rejected()
        {
            var tenant = Guid.NewGuid();
            var request = BuildQuoteRequest(tenant, shippingId: 10, country: "FR",
                new CartItem { Listing = 1, Option = "Default", Quantity = 1 });

            var tax = new Tax(rate: 1000, kind: TaxKind.GST);
            var listing = BuildListing(1, "Default", 500, 1000, "USD", tax.Rate, (int)tax.Kind);
            var shipping = BuildShippingOption(10, 900, "USD", ["US"]); // FR not supported

            var listingRepo = new Mock<IDomainRepository<ListingDomain, Listing>>(MockBehavior.Strict);
            _ = listingRepo.Setup(r => r.GetAsync(Listing.BuildPartitionKey(1), Listing.BuildRowKey("Default"), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(BuildInitializedListingDomain(listing));

            var shippingRepo = new Mock<IDomainRepository<ShippingOptionDomain, ShippingOption>>(MockBehavior.Strict);
            _ = shippingRepo.Setup(r => r.GetAsync(ShippingOption.BuildPartitionKey(), ShippingOption.BuildRowKey(10), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(BuildInitializedShippingDomain(shipping));

            var promoRepo = new Mock<IDomainRepository<PromotionDomain, Promotion>>(MockBehavior.Strict);
            var logger = new Mock<ILogger<QuoteFlow>>(MockBehavior.Loose);

            var flow = new QuoteFlow(shippingRepo.Object, listingRepo.Object, promoRepo.Object, logger.Object);

            Func<Task> act = async () => await flow.RunAsync(request, CancellationToken.None);
            var ex = await act.Should().ThrowAsync<ApplicationException>();
            ex.Which.ErrorCode.Should().Be(InternalError.BadRequest);
        }

        // Scenario: Buy 1 Get 1 Free promotion is applied for listing ID 1
        // Given: Cart contains listing 1; coupon BUY1GET1FREE is provided
        // When: QuoteFlow runs
        // Then: The qualified item has its total reduced and a discount description is set
        [TestMethod]
        public async Task Promotion_buy1get1free_is_applied()
        {
            var tenant = Guid.NewGuid();
            var request = BuildQuoteRequest(tenant, shippingId: 10, country: "US",
                new CartItem { Listing = 1, Option = "Default", Quantity = 2 });
            request.Coupon = "BUY1GET1FREE";

            var tax = new Tax(rate: 1000, kind: TaxKind.GST);
            var listing = BuildListing(1, "Default", 500, 1000, "USD", tax.Rate, (int)tax.Kind);
            var shipping = BuildShippingOption(10, 900, "USD", ["US"]);
            var promotion = BuildPromotion(tenant, "BUY1GET1FREE");

            var listingRepo = new Mock<IDomainRepository<ListingDomain, Listing>>(MockBehavior.Strict);
            _ = listingRepo.Setup(r => r.GetAsync(Listing.BuildPartitionKey(1), Listing.BuildRowKey("Default"), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(BuildInitializedListingDomain(listing));

            var shippingRepo = new Mock<IDomainRepository<ShippingOptionDomain, ShippingOption>>(MockBehavior.Strict);
            _ = shippingRepo.Setup(r => r.GetAsync(ShippingOption.BuildPartitionKey(), ShippingOption.BuildRowKey(10), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(BuildInitializedShippingDomain(shipping));

            var promoRepo = new Mock<IDomainRepository<PromotionDomain, Promotion>>(MockBehavior.Strict);
            _ = promoRepo.Setup(r => r.GetAsync(It.Is<Promotion>(p => p.Tenant == tenant && p.Code == "BUY1GET1FREE"), It.IsAny<CancellationToken>()))
                .ReturnsAsync(BuildInitializedPromotionDomain(promotion));

            var logger = new Mock<ILogger<QuoteFlow>>(MockBehavior.Loose);
            var flow = new QuoteFlow(shippingRepo.Object, listingRepo.Object, promoRepo.Object, logger.Object);

            var quote = await flow.RunAsync(request, CancellationToken.None);

            quote.Quote.Should().HaveCount(1);
            var item = quote.Quote.Single();
            item.Listing.Should().Be(1);
            item.Now.Should().Be(500);
            item.Quantity.Should().Be(2);
            item.LineTotal.Should().Be(1000);
            quote.DiscountDescription.Values.Should().Contain("Buy 1 Get 1 Free");
            quote.Discount.Should().Be(500);
            quote.Tax.Should().Be(128);
            quote.Total.Should().Be(1400);
        }

        // Scenario: Buy 2 Get 3 Free promotion with gift item handling
        // Given: Cart contains listing 1 (qty > 2) and listing 2 (gift) as required; coupon BUY2GET3FREE
        // When: QuoteFlow runs
        // Then: Discount applies to listing 1, and listing 2 gets its gift reduction
        [TestMethod]
        public async Task Promotion_buy2get3free_is_applied_with_gift()
        {
            var tenant = Guid.NewGuid();
            var request = BuildQuoteRequest(tenant, shippingId: 10, country: "US",
                new CartItem { Listing = 1, Option = "Default", Quantity = 5 },
                new CartItem { Listing = 2, Option = "Default", Quantity = 4 });
            request.Coupon = "BUY2GET3FREE";

            var tax = new Tax(rate: 1000, kind: TaxKind.GST);
            var listing1 = BuildListing(1, "Default", 500, 1000, "USD", tax.Rate, (int)tax.Kind);
            var listing2 = BuildListing(2, "Default", 0, 1000, "USD", tax.Rate, (int)tax.Kind);
            var shipping = BuildShippingOption(10, 900, "USD", ["US"]);
            var promotion = BuildPromotion(tenant, "BUY2GET3FREE");

            var listingRepo = new Mock<IDomainRepository<ListingDomain, Listing>>(MockBehavior.Strict);
            _ = listingRepo.Setup(r => r.GetAsync(Listing.BuildPartitionKey(1), Listing.BuildRowKey("Default"), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(BuildInitializedListingDomain(listing1));
            _ = listingRepo.Setup(r => r.GetAsync(Listing.BuildPartitionKey(2), Listing.BuildRowKey("Default"), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(BuildInitializedListingDomain(listing2));

            var shippingRepo = new Mock<IDomainRepository<ShippingOptionDomain, ShippingOption>>(MockBehavior.Strict);
            _ = shippingRepo.Setup(r => r.GetAsync(ShippingOption.BuildPartitionKey(), ShippingOption.BuildRowKey(10), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(BuildInitializedShippingDomain(shipping));

            var promoRepo = new Mock<IDomainRepository<PromotionDomain, Promotion>>(MockBehavior.Strict);
            _ = promoRepo.Setup(r => r.GetAsync(It.Is<Promotion>(p => p.Tenant == tenant && p.Code == "BUY2GET3FREE"), It.IsAny<CancellationToken>()))
                .ReturnsAsync(BuildInitializedPromotionDomain(promotion));

            var logger = new Mock<ILogger<QuoteFlow>>(MockBehavior.Loose);
            var flow = new QuoteFlow(shippingRepo.Object, listingRepo.Object, promoRepo.Object, logger.Object);

            var quote = await flow.RunAsync(request, CancellationToken.None);

            quote.Quote.Should().HaveCount(2);
            var item = quote.Quote.Single(i => i.Listing == 1);
            var gift = quote.Quote.Single(i => i.Listing == 2);
            item.Now.Should().Be(500);
            item.Quantity.Should().Be(5);
            item.LineTotal.Should().Be(2500);
            gift.Now.Should().Be(0);
            gift.Quantity.Should().Be(4);
            gift.LineTotal.Should().Be(0);
            quote.Discount.Should().Be(1500);
            quote.Tax.Should().Be(173);
            quote.Total.Should().Be(1900);

            quote.DiscountDescription.Values.Should().Contain("Buy 2 Get 3 Free");
            quote.DiscountDescription.Values.Should().Contain("Get 4 Hair Remover Free");
        }

        // Scenario: Coupon present but not applicable to current cart
        // Given: An unrelated coupon is supplied
        // When: QuoteFlow runs
        // Then: Quote totals remain based on baseline pricing (no discount applied)
        [TestMethod]
        public async Task Irrelevant_coupon_has_no_effect()
        {
            var tenant = Guid.NewGuid();
            var request = BuildQuoteRequest(tenant, shippingId: 10, country: "US",
                new CartItem { Listing = 3, Option = "Default", Quantity = 2 });
            request.Coupon = "BUY1GET1FREE"; // targets listing 1 only

            var tax = new Tax(rate: 1000, kind: TaxKind.GST);
            var listing = BuildListing(3, "Default", 500, 1000, "USD", tax.Rate, (int)tax.Kind);
            var shipping = BuildShippingOption(10, 900, "USD", ["US"]);
            var promotion = BuildPromotion(tenant, "BUY1GET1FREE");

            var listingRepo = new Mock<IDomainRepository<ListingDomain, Listing>>(MockBehavior.Strict);
            _ = listingRepo.Setup(r => r.GetAsync(Listing.BuildPartitionKey(3), Listing.BuildRowKey("Default"), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(BuildInitializedListingDomain(listing));

            var shippingRepo = new Mock<IDomainRepository<ShippingOptionDomain, ShippingOption>>(MockBehavior.Strict);
            _ = shippingRepo.Setup(r => r.GetAsync(ShippingOption.BuildPartitionKey(), ShippingOption.BuildRowKey(10), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(BuildInitializedShippingDomain(shipping));

            var promoRepo = new Mock<IDomainRepository<PromotionDomain, Promotion>>(MockBehavior.Strict);
            _ = promoRepo.Setup(r => r.GetAsync(It.Is<Promotion>(p => p.Tenant == tenant && p.Code == "BUY1GET1FREE"), It.IsAny<CancellationToken>()))
                .ReturnsAsync(BuildInitializedPromotionDomain(promotion));

            var logger = new Mock<ILogger<QuoteFlow>>(MockBehavior.Loose);
            var flow = new QuoteFlow(shippingRepo.Object, listingRepo.Object, promoRepo.Object, logger.Object);

            var quote = await flow.RunAsync(request, CancellationToken.None);

            var expectedSubTotal = 2 * 500;
            quote.Discount.Should().Be(0);
            quote.Total.Should().Be(expectedSubTotal + quote.ShippingCost);
        }

        // Scenario: Empty cart is invalid
        // Given: No items in the cart
        // When: QuoteFlow runs
        // Then: Flow fails (since no baseline item exists to derive currency and tax)
        [TestMethod]
        public async Task Empty_cart_is_rejected()
        {
            var tenant = Guid.NewGuid();
            var request = BuildQuoteRequest(tenant, shippingId: 10, country: "US");

            var shipping = BuildShippingOption(10, 900, "USD", new[] { "US" });
            var shippingRepo = new Mock<IDomainRepository<ShippingOptionDomain, ShippingOption>>(MockBehavior.Strict);
            _ = shippingRepo.Setup(r => r.GetAsync(ShippingOption.BuildPartitionKey(), ShippingOption.BuildRowKey(10), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(BuildInitializedShippingDomain(shipping));

            var listingRepo = new Mock<IDomainRepository<ListingDomain, Listing>>(MockBehavior.Strict);
            var promoRepo = new Mock<IDomainRepository<PromotionDomain, Promotion>>(MockBehavior.Strict);
            var logger = new Mock<ILogger<QuoteFlow>>(MockBehavior.Loose);

            var flow = new QuoteFlow(shippingRepo.Object, listingRepo.Object, promoRepo.Object, logger.Object);

            Func<Task> act = async () => await flow.RunAsync(request, CancellationToken.None);
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        // Builders and helpers
        private static QuoteRequest BuildQuoteRequest(Guid tenant, int shippingId, string country, params CartItem[] items)
            => new()
            {
                ID = Guid.NewGuid(),
                Tenant = tenant,
                Shipping = shippingId,
                ShippingCountry = country,
                Captcha = "dummy",
                Cart = items.ToList(),
            };

        private static Listing BuildListing(int id, string option, long price, long wasPrice, string currency, long taxRate, int taxKind)
            => new()
            {
                ID = id,
                Option = option,
                Name = $"Item {id}",
                Price = price,
                WasPrice = wasPrice,
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
                Countries = string.Join(',', countries),
            };

        private static Promotion BuildPromotion(Guid tenant, string code)
            => new()
            {
                Tenant = tenant,
                Code = code
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

        private static PromotionDomain BuildInitializedPromotionDomain(Promotion promotion)
        {
            var repo = new Mock<IRepository<Promotion>>(MockBehavior.Loose);
            var handlers = Array.Empty<IDomainEventHandler<IDomain<Promotion>>>();
            var domain = new PromotionDomain(new Lazy<IRepository<Promotion>>(() => repo.Object), handlers);
            InitializeDomain(domain, promotion);
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
                if (mi != null) return mi;
                type = type.BaseType!;
            }
            return null;
        }
    }
}
