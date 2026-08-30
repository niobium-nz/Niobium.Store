using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;
using Niobium.Store.Server;

FunctionsApplication.CreateBuilder(args)
    .ConfigureFunctionsWebApplication()
    .AddStore()
    .UseStore()
    .Build()
    .Run();
