namespace Cronus.Ordering.Data;

/// <summary>Populates the sample catalogue for a one-off job.</summary>
/// <remarks>
/// Outside Development, seeding is explicit so restarts cannot introduce sample data.
/// Use the runtime identity: seeding writes rows and needs no migration privileges.
/// The seeder skips insertion once restaurants exist, making repeated runs safe.
/// </remarks>
internal static class SeedCommand
{
    /// <summary>The argument that selects this mode instead of serving requests.</summary>
    internal const string Name = "seed";

    /// <summary>Category its log lines appear under, so they are recognisable in a job's output.</summary>
    internal const string LogCategory = "Cronus.Ordering.Seed";

    /// <summary>
    /// Whether this invocation is the seeding job rather than the service.
    /// </summary>
    public static bool IsRequested(string[] args) =>
        args.Any(argument => string.Equals(argument, Name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Writes the sample catalogue if the database holds no restaurants.
    /// </summary>
    /// <returns>0 when the catalogue is present, 1 when it could not be written.</returns>
    public static async Task<int> RunAsync(
        OrderingDbContext db,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (await DevelopmentDataSeeder.SeedAsync(db, cancellationToken))
            {
                logger.LogInformation("Seeded the sample catalogue.");
            }
            else
            {
                logger.LogInformation("The catalogue already holds restaurants, so nothing was seeded.");
            }

            return 0;
        }
        catch (Exception exception)
        {
            // A failed seed has to fail the job, or an environment that was never populated would
            // look as though it had been.
            logger.LogError(exception, "Seeding the catalogue failed.");
            return 1;
        }
    }
}
