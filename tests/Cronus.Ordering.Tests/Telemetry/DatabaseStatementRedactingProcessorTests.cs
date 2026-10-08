using System.Diagnostics;
using Cronus.Ordering.Telemetry;

namespace Cronus.Ordering.Tests.Telemetry;

/// <summary>
/// Covers what the redacting processor does to a finished database span: the statement text goes,
/// everything that describes the dependency stays.
/// </summary>
/// <remarks>
/// The spans here are built by hand rather than produced by Npgsql. The processor only ever sees a
/// finished <see cref="Activity"/>, so a hand-built one exercises exactly the same input, and the
/// test does not need a database to prove what the processor does with a tag.
/// </remarks>
[TestClass]
public sealed class DatabaseStatementRedactingProcessorTests
{
    private readonly DatabaseStatementRedactingProcessor _processor = new();

    private const string StatementText = "SELECT o.\"Id\" FROM \"Orders\" AS o WHERE o.\"Id\" = @__id_0";

    [TestMethod]
    public void OnEnd_RemovesTheStatementText()
    {
        using var activity = new Activity("postgresql").SetTag("db.query.text", StatementText);

        _processor.OnEnd(activity);

        Assert.IsNull(activity.GetTagItem("db.query.text"));
    }

    [TestMethod]
    public void OnEnd_RemovesTheSupersededStatementTag()
    {
        // The tag was renamed when the database semantic conventions were revised. Clearing the old
        // name as well means a driver that reverts, or instrumentation that still uses it, cannot
        // start leaking the text again without a test failing.
        using var activity = new Activity("postgresql").SetTag("db.statement", StatementText);

        _processor.OnEnd(activity);

        Assert.IsNull(activity.GetTagItem("db.statement"));
    }

    [TestMethod]
    public void OnEnd_KeepsWhatDescribesTheDependency()
    {
        using var activity = new Activity("postgresql")
            .SetTag("db.system.name", "postgresql")
            .SetTag("db.namespace", "cronus_dev_ordering")
            .SetTag("server.address", "psql-cronus-nonprod.postgres.database.azure.com")
            .SetTag("db.query.text", StatementText);

        _processor.OnEnd(activity);

        // Redaction must preserve the database identity needed to diagnose slow dependencies.
        Assert.AreEqual("postgresql", activity.GetTagItem("db.system.name"));
        Assert.AreEqual("cronus_dev_ordering", activity.GetTagItem("db.namespace"));
        Assert.AreEqual(
            "psql-cronus-nonprod.postgres.database.azure.com",
            activity.GetTagItem("server.address"));
    }

    [TestMethod]
    public void OnEnd_OnASpanWithoutDatabaseTags_ChangesNothing()
    {
        using var activity = new Activity("GET /restaurants").SetTag("http.request.method", "GET");

        _processor.OnEnd(activity);

        // The processor sees every span in the process, not only database ones.
        Assert.AreEqual("GET", activity.GetTagItem("http.request.method"));
        Assert.AreEqual(1, activity.TagObjects.Count());
    }
}
