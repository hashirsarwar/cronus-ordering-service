using System.Text.Json.Serialization;
using Cronus.Ordering.Data;
using Cronus.Ordering.Endpoints;
using Cronus.Ordering.Errors;
using Cronus.Ordering.Health;
using Cronus.Ordering.Services;
using Cronus.Ordering.Telemetry;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Register instrumentation early; no connection string keeps local runs free of Azure.
var applicationInsightsConnectionString =
    builder.Configuration[ApplicationTelemetry.ConnectionStringKey];
builder.Services.AddApplicationTelemetry(applicationInsightsConnectionString);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Fail at start-up with an actionable message rather than on the first request.
var connectionString = builder.Configuration.GetConnectionString("CronusOrdering");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'CronusOrdering' is not configured. Set ConnectionStrings:CronusOrdering in " +
        "appsettings.Development.json, or the ConnectionStrings__CronusOrdering environment variable.");
}

// Default to Entra authentication so deployments cannot use a stored password by omission.
// Development overrides this for local PostgreSQL.
var useEntraAuthentication = builder.Configuration.GetValue("Database:UseEntraAuthentication", true);

// Reject conflicting authentication settings before the first connection attempt.
PostgresDataSourceFactory.Validate(connectionString, useEntraAuthentication);

// Resolve the host logger through DI and let the singleton dispose its connection pool on shutdown.
builder.Services.AddSingleton(serviceProvider => PostgresDataSourceFactory.Create(
    connectionString,
    useEntraAuthentication,
    serviceProvider.GetRequiredService<ILoggerFactory>()));

builder.Services.AddDbContext<OrderingDbContext>((serviceProvider, options) =>
    options.UseNpgsql(serviceProvider.GetRequiredService<NpgsqlDataSource>()));

// Handle migrations before validating the delivery URL: schema changes need only the database.
if (MigrationCommand.IsRequested(args))
{
    await using var migrationHost = builder.Build();
    await using var migrationScope = migrationHost.Services.CreateAsyncScope();
    var migrationServices = migrationScope.ServiceProvider;

    return await MigrationCommand.RunAsync(
        migrationServices.GetRequiredService<OrderingDbContext>(),
        migrationServices.GetRequiredService<ILoggerFactory>().CreateLogger(MigrationCommand.LogCategory));
}

// Like migrations, catalogue seeding needs no delivery service configuration.
if (SeedCommand.IsRequested(args))
{
    await using var seedHost = builder.Build();
    await using var seedScope = seedHost.Services.CreateAsyncScope();
    var seedServices = seedScope.ServiceProvider;

    return await SeedCommand.RunAsync(
        seedServices.GetRequiredService<OrderingDbContext>(),
        seedServices.GetRequiredService<ILoggerFactory>().CreateLogger(SeedCommand.LogCategory));
}

var deliveryServiceBaseUrl = builder.Configuration["DeliveryService:BaseUrl"];
if (string.IsNullOrWhiteSpace(deliveryServiceBaseUrl))
{
    throw new InvalidOperationException(
        "Configuration 'DeliveryService:BaseUrl' is not set. Set DeliveryService:BaseUrl in " +
        "appsettings.Development.json, or the DeliveryService__BaseUrl environment variable.");
}

builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<OrderService>();

// Exclude delivery from readiness: orders are still accepted when delivery is unavailable.
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("postgresql", tags: [HealthEndpoints.ReadyTag]);

builder.Services.AddHttpClient<DeliveryClient>(client =>
{
    client.BaseAddress = new Uri(deliveryServiceBaseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue("DeliveryService:TimeoutSeconds", 10));
});

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

// Distinguish missing telemetry configuration from configured telemetry that fails to export.
app.Logger.LogInformation(
    "Application telemetry is {TelemetryState} for {ServiceName}.",
    ApplicationTelemetry.IsConfigured(applicationInsightsConnectionString) ? "enabled" : "disabled",
    ApplicationTelemetry.ServiceName);

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthEndpoints();
app.MapRestaurantEndpoints();
app.MapCartEndpoints();
app.MapOrderEndpoints();

// Convenience so a fresh clone runs with one command. Migrations remain the source of truth,
// and the seeder is idempotent, so this is safe to repeat on every start-up.
if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
    await db.Database.MigrateAsync();
    await DevelopmentDataSeeder.SeedAsync(db);
}

app.Run();


return 0;
