using System;
using Newtonsoft.Json;
using ReiEditor.Models.Services.Components;

namespace ReiEditor.Models.Services.Entities;

public class GameEntity
{
    public event Action<GameEntity, string>? NameChangedEvent;
    
    [JsonProperty]
    public int Id { get; }

    [JsonProperty]
    public string Name { get; private set; }

    [JsonProperty]
    public TransformComponent Transform { get; private set; }

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

    public bool Equals(GameEntity other) => Id == other.Id;
	
    public override string ToString()
    {
        return $"E({Name}:{Id})";
    }
}