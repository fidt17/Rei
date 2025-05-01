using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using ReiEditor.Models.Services.Components;

namespace ReiEditor.Models.Services.Entities;

public class GameEntity
{
    public event Action<GameEntity, string>? NameChangedEvent;
    public event Action<GameEntity, BehaviourComponent>? BehaviourAddedEvent;
    public event Action<GameEntity, BehaviourComponent>? BehaviourDeletedEvent;
    
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

    public bool HasComponent(int id) => _behaviours.Exists(x => x.Id == id);
    public bool HasBehaviour(BehaviourComponent behaviour) => _behaviours.Contains(behaviour);

    public void AddBehaviour(BehaviourComponent behaviour)
    {
        if (_behaviours.Contains(behaviour)) throw new Exception("Trying to add same behaviour twice");
        
        _behaviours.Add(behaviour);
        BehaviourAddedEvent?.Invoke(this, behaviour);
    }

    public void DeleteBehaviour(BehaviourComponent behaviour)
    {
        if (!_behaviours.Contains(behaviour)) throw new Exception("Entity does not has such behaviour");

        _behaviours.Remove(behaviour);
        BehaviourDeletedEvent?.Invoke(this, behaviour);
    }

    public BehaviourComponent? GetBehaviour(int? id) => id == null ? null : _behaviours.Find(x => x.Id == id);

    public bool Equals(GameEntity other) => Id == other.Id;
	
    public override string ToString()
    {
        return $"E({Name}:{Id})";
    }
}