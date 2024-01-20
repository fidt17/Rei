using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using ReiEditor.Models.Services.Components;

namespace ReiEditor.Models.Services.Entities;

public class GameEntity
{
    public event Action<GameEntity, string>? NameChangedEvent;
    public event Action<GameEntity, BehaviourComponent>? BehaviourAddedEvent;
    
    [JsonProperty]
    public int Id { get; }

    [JsonProperty]
    public string Name { get; private set; }

    [JsonProperty]
    public TransformComponent Transform { get; private set; }

    public IEnumerable<BehaviourComponent> Behaviours => _behaviours;

    private readonly List<BehaviourComponent> _behaviours = new();

    public GameEntity(int id, string name)
    {
        Id = id;
        Name = name;
        Transform = new TransformComponent();
    }

    public void SetName(string name)
    {
        if (Name == name) return;
        Name = name;
        NameChangedEvent?.Invoke(this, Name);
    }

    public void AddBehaviour(BehaviourComponent behaviour)
    {
        _behaviours.Add(behaviour);
        BehaviourAddedEvent?.Invoke(this, behaviour);
    }

    public bool Equals(GameEntity other) => Id == other.Id;
	
    public override string ToString()
    {
        return $"E({Name}:{Id})";
    }
}