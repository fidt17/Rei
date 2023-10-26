using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Entities;

namespace ReiEditor.Models.Services.Scenes;

public class Scene : Asset
{
	[JsonProperty]
	public string Name { get; }

	[JsonIgnore]
	public IEnumerable<GameEntity> Entities => _entities;

	[JsonProperty("Entities")]
	private List<GameEntity> _entities { get; } = new();

	public Scene(string name)
	{
		Name = name;
	}

	public int AllocateEntityId() => _entities.Count == 0 ? 1 : _entities.Max(x => x.Id) + 1;

	public void AddEntity(GameEntity entity)
	{
		if (_entities.Exists(x => x.Equals(entity))) throw new Exception($"Entity with Id {entity.Id} already exists in scene");

		_entities.Add(entity);
	}

	public void DeleteEntity(GameEntity entity)
	{
		_entities.Remove(entity);
	}
}