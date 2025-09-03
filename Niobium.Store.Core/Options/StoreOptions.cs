namespace Niobium.Store.Options
{
    public class StoreOptions
    {
        public required Dictionary<string, Guid> Tenants { get; set; }

        public required Guid InvoicingTenant { get; set; }
    }
}
