using Cronus.Ordering.Data;
using Cronus.Ordering.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cronus.Ordering.Tests.Data;

/// <summary>
/// The seeding job decides whether an environment got a catalogue from this command's exit code, so
/// these tests cover the argument that selects the mode, what it writes, and what it reports.
/// </summary>
[TestClass]
public sealed class SeedCommandTests
{
    [TestMethod]
    public void IsRequested_WhenTheArgumentIsSeed_ReturnsTrue() =>
        Assert.IsTrue(SeedCommand.IsRequested(["seed"]));

    [TestMethod]
    public void IsRequested_IsCaseInsensitive() =>
        Assert.IsTrue(SeedCommand.IsRequested(["SEED"]));

    [TestMethod]
    public void IsRequested_WhenSeedAccompaniesOtherArguments_ReturnsTrue() =>
        Assert.IsTrue(SeedCommand.IsRequested(["--urls", "http://+:8080", "seed"]));

    [TestMethod]
    public void IsRequested_WhenNothingSelectsSeeding_ReturnsFalse() =>
        Assert.IsFalse(SeedCommand.IsRequested([]));

    /// <summary>
    /// Only the exact token counts, so an argument that merely resembles the mode starts the service
    /// rather than quietly writing sample data.
    /// </summary>
    [TestMethod]
    public void IsRequested_WhenAnArgumentMerelyResemblesSeed_ReturnsFalse() =>
        Assert.IsFalse(SeedCommand.IsRequested(["seeds", "--seed-once", "migrate"]));

    [TestMethod]
    public async Task RunAsync_SeedsAnEmptyCatalogue()
    {
        // The shared database is emptied first, so this asserts the empty case rather than whatever
        // an earlier test left behind.
        await using var db = await TestDatabase.CreateCleanContextAsync();

        var exitCode = await SeedCommand.RunAsync(db, NullLogger.Instance);

        Assert.AreEqual(0, exitCode);
        Assert.IsTrue(await db.Restaurants.AnyAsync(), "the catalogue should have been seeded");
        Assert.IsTrue(await db.MenuItems.AnyAsync(), "the seeded restaurants should have menus");

        await TestDatabase.ResetAsync();
    }

    /// <summary>
    /// Running it twice must not duplicate the catalogue, because a job is re-run whenever an operator
    /// applies it again.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_IsIdempotent()
    {
        await using var db = await TestDatabase.CreateCleanContextAsync();

        await SeedCommand.RunAsync(db, NullLogger.Instance);
        var afterFirst = await db.Restaurants.CountAsync();

        var exitCode = await SeedCommand.RunAsync(db, NullLogger.Instance);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(afterFirst, await db.Restaurants.CountAsync());

        await TestDatabase.ResetAsync();
    }

    /// <summary>
    /// A seed that cannot be written has to fail the job, or an environment that was never populated
    /// would look as though it had been.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_WhenTheDatabaseIsUnreachable_ReturnsFailure()
    {
        await using var db = new OrderingDbContext(
            new DbContextOptionsBuilder<OrderingDbContext>()
                .UseNpgsql(TestDatabase.UnreachableConnectionString)
                .Options);

        var exitCode = await SeedCommand.RunAsync(db, NullLogger.Instance);

        Assert.AreEqual(1, exitCode);
    }
}
