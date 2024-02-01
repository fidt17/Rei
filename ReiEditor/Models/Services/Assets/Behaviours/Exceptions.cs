using System;

namespace ReiEditor.Models.Services.Assets.Behaviours;

public class UnregisteredBehaviourException : Exception
{
    private readonly int _componentId;

    public UnregisteredBehaviourException(int componentId)
    {
        _componentId = componentId;
    }

    public override string Message => $"Component with ID {_componentId} has not been registered";
}