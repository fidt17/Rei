using ReiEditor.Models.EditorApp.EditorProcedures;
using ReiEditor.Utils.Common.Procedures;

namespace ReiEditor.Tests.Models.EditorApp.EditorProcedures;

/// <summary>
/// Verifies active procedure tracking and event state across independent procedure lifetimes.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "EditorProcedures")]
public sealed class EditorProceduresServiceTests
{
    /// <summary>
    /// Tracking an already completed procedure leaves an empty service unchanged and publishes no events.
    /// </summary>
    [Fact]
    public void FinishedProcedureIsIgnored()
    {
        var service = new EditorProceduresService();
        var procedure = new Procedure("Already finished");
        procedure.Complete();
        var eventCount = 0;
        service.ProcedureStartedEvent += _ => eventCount++;
        service.ProcedureFinishedEvent += _ => eventCount++;

        service.TrackProcedure(procedure);

        Assert.Empty(service.ActiveProcedures);
        Assert.False(service.AnyActiveProcedures());
        Assert.Equal(0, eventCount);
    }

    /// <summary>
    /// Start and finish events carry the procedure after the active set has been updated.
    /// </summary>
    [Fact]
    public void TrackingPublishesUpdatedStateAtEachLifecycleEvent()
    {
        var service = new EditorProceduresService();
        var procedure = new Procedure("Build");
        var started = new List<IProcedure>();
        var finished = new List<IProcedure>();
        service.ProcedureStartedEvent += value =>
        {
            Assert.True(service.AnyActiveProcedures());
            Assert.Same(value, Assert.Single(service.ActiveProcedures));
            started.Add(value);
        };
        service.ProcedureFinishedEvent += value =>
        {
            Assert.True(value.Finished);
            Assert.False(service.AnyActiveProcedures());
            Assert.Empty(service.ActiveProcedures);
            finished.Add(value);
        };

        service.TrackProcedure(procedure);
        Assert.Same(procedure, Assert.Single(started));
        Assert.Empty(finished);
        procedure.Complete();

        Assert.Same(procedure, Assert.Single(finished));
    }

    /// <summary>
    /// Completing one of several active procedures retains the others and reports each completion once.
    /// </summary>
    [Fact]
    public void ProceduresCanFinishOutOfStartOrder()
    {
        var service = new EditorProceduresService();
        var first = new Procedure("First");
        var second = new Procedure("Second");
        var finished = new List<IProcedure>();
        service.ProcedureFinishedEvent += finished.Add;
        service.TrackProcedure(first);
        service.TrackProcedure(second);

        second.Complete();

        Assert.True(service.AnyActiveProcedures());
        Assert.Same(first, Assert.Single(service.ActiveProcedures));
        Assert.Equal(new IProcedure[] { second }, finished);
        first.Complete();

        Assert.Empty(service.ActiveProcedures);
        Assert.False(service.AnyActiveProcedures());
        Assert.Equal(new IProcedure[] { second, first }, finished);
    }
}
