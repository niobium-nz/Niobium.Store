using Niobium.Store.Host;
WebApplication.CreateBuilder(args)
    .AddStore()
    .Build()
    .UseStore()
    .Run();
