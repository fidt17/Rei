using System;

namespace ReiEditor.Utils.Common.Procedures;

public interface IProcedure
{
    event Action FinishedEvent;
    event Action ProgressChangedEvent;

    string Name { get; }
    bool Finished { get; }
    double? Progress { get; }
}
