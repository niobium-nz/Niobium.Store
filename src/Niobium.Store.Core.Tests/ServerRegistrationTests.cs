using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Niobium.Finance;
using Niobium.Platform;

namespace Niobium.Store.Core.Tests;

[TestClass]
public sealed class ServerRegistrationTests
{
    [TestMethod]
    public void Store_host_resolves_the_financial_query_repositories()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = Environments.Production,
            Args = [],
        });
        Niobium.Store.Server.DependencyModule.AddStore(builder);
        using ServiceProvider services = builder.Services.BuildServiceProvider();

        // Customer creation first resolves these lazily while initializing its balance.
        // Merely constructing OrderDomain or validating the container misses this failure.
        Assert.IsNotNull(services.GetRequiredService<Lazy<IQueryableRepository<Accounting>>>().Value);
        Assert.IsNotNull(services.GetRequiredService<Lazy<IQueryableRepository<Transaction>>>().Value);
        Assert.IsNotNull(services.GetRequiredService<IRepository<Ownership>>());
    }
}
