namespace ReiEditor.Models.Services.Assets.Creation.Behaviour;

public readonly struct BehaviourCreationSettings
{
    public string TargetDirectory { get; }
    public string BehaviourName { get; }
    public bool OverrideInit { get; }
    public bool OverrideStart { get; }
    public bool OverrideUpdate { get; }
    public bool OverrideDispose { get; }

    public BehaviourCreationSettings(
        string targetDirectory,
        string behaviourName,
        bool overrideInit,
        bool overrideStart,
        bool overrideUpdate,
        bool overrideDispose)
    {
        TargetDirectory = targetDirectory.Trim();
        BehaviourName = behaviourName.Trim();
        OverrideInit = overrideInit;
        OverrideStart = overrideStart;
        OverrideUpdate = overrideUpdate;
        OverrideDispose = overrideDispose;
    }
}
