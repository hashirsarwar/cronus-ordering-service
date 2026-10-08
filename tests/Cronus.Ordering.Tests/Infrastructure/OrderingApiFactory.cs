using Cronus.Ordering.Data;
using Cronus.Ordering.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cronus.Ordering.Tests.Infrastructure;

/// <summary>Boots the real host against the test database.</summary>
/// <remarks>
/// Program reads settings before WebApplicationFactory contributes configuration sources, so use environment variables.
/// The Testing environment skips Development-only database initialization, leaving the suite in control.
/// </remarks>
internal sealed class OrderingApiFactory(
    HttpMessageHandler? deliveryHandler = null,
    string? databaseConnectionString = null) : WebApplicationFactory<Program>
{
    /// <summary>Stand-in address for the delivery service. Requests never leave the process.</summary>
    public const string DeliveryBaseUrl = "http://delivery.test";

    /// <summary>Creates a host whose database cannot be reached, to simulate an outage.</summary>
    public static OrderingApiFactory WithUnreachableDatabase() =>
        new(databaseConnectionString: TestDatabase.UnreachableConnectionString);

    static OrderingApiFactory()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
        Environment.SetEnvironmentVariable("ConnectionStrings__CronusOrdering", TestDatabase.ConnectionString);
        Environment.SetEnvironmentVariable("DeliveryService__BaseUrl", DeliveryBaseUrl);

        // Testing has no Development override, so disable the default Entra mode for local PostgreSQL.
        Environment.SetEnvironmentVariable("Database__UseEntraAuthentication", "false");

        // Clear inherited telemetry configuration so test traffic cannot reach a real Azure resource.
        Environment.SetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING", null);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        if (databaseConnectionString is not null)
        {
            // Remove existing options first: AddDbContext uses TryAdd and would retain the original registration.
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<OrderingDbContext>>();
                services.AddDbContext<OrderingDbContext>(options =>
                    options.UseNpgsql(databaseConnectionString));
            });
        }

        if (deliveryHandler is null)
        {
            return;
        }

        // Only the transport is replaced, so the real DeliveryClient is still exercised, including
        // its serialization and error mapping.
        builder.ConfigureServices(services => services
            .AddHttpClient<DeliveryClient>()
            .ConfigurePrimaryHttpMessageHandler(() => deliveryHandler));
    }
}
