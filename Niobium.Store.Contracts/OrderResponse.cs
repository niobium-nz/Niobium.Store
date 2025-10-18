using System.Diagnostics.CodeAnalysis;

namespace Niobium.Store
{
    [method: SetsRequiredMembers]
    public class OrderResponse() : Order
    {
        public required long Order { get; set; }

        public required string Instruction { get; set; }

        public static OrderResponse Map(Order order) => new()
        {
            Order = order.GetID(),
            Customer = order.Customer,
            Created = order.Created,
            Timestamp = order.Timestamp,
            ETag = order.ETag,
            Status = order.Status,
            Settled = order.Settled,
            Total = order.Total,
            Discount = order.Discount,
            ShippingCost = order.ShippingCost,
            Tax = order.Tax,
            TaxRate = order.TaxRate,
            TaxKind = order.TaxKind,
            Coupon = order.Coupon,
            Notes = order.Notes,
            Tenant = order.Tenant,
            Cart = order.Cart,
            Currency = order.Currency,
            Culture = order.Culture,
            TimeZone = order.TimeZone,
            Consignee = order.Consignee,
            Email = order.Email,
            Phone = order.Phone,
            ShippingStatus = order.ShippingStatus,
            ShippingAddressLine1 = order.ShippingAddressLine1,
            ShippingAddressLine2 = order.ShippingAddressLine2,
            ShippingCity = order.ShippingCity,
            ShippingState = order.ShippingState,
            ShippingPostcode = order.ShippingPostcode,
            ShippingCountry = order.ShippingCountry,
            BillingAddressLine1 = order.BillingAddressLine1,
            BillingAddressLine2 = order.BillingAddressLine2,
            BillingSuburb = order.BillingSuburb,
            BillingCity = order.BillingCity,
            BillingState = order.BillingState,
            BillingPostcode = order.BillingPostcode,
            BillingCountry = order.BillingCountry,
            BillingBusiness = order.BillingBusiness,
            BillingName = order.BillingName,
            MarketingSubscription = order.MarketingSubscription,
            ShippingSuburb = order.ShippingSuburb,
        };
    }
}
