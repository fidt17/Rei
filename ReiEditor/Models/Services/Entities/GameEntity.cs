using Newtonsoft.Json;

namespace ReiEditor.Models.Services.Entities;

public class GameEntity
{
	[JsonProperty]
	public int Id { get; }

	[JsonProperty]
	public string Name { get; }

	public GameEntity(int id, string name)
	{
		Id = id;
		Name = name;
	}

	public bool Equals(GameEntity other) => Id == other.Id;
	
	public override string ToString()
	{
		return $"E({Name}:{Id})";
	}
}