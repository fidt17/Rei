using ReiEditor.Utils.Common.Procedures;

namespace ReiEditor.Tests.Utils.Common;

/// <summary>
/// Verifies procedure identity and completion state visible to subscribers.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "Common")]
public sealed class ProcedureTests
{
    /// <summary>
    /// A procedure preserves its name and becomes finished before notifying completion subscribers.
    /// </summary>
    [Fact]
    public void CompletePublishesFinishedState()
    {
        var procedure = new Procedure("Import assets");
        var observedStates = new List<bool>();
        procedure.FinishedEvent += () => observedStates.Add(procedure.Finished);
        Assert.Equal("Import assets", procedure.Name);
        Assert.False(procedure.Finished);

        procedure.Complete();

        Assert.True(procedure.Finished);
        Assert.Equal(new[] { true }, observedStates);
    }
}
