using System.Collections.Generic;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Scenes;

namespace ReiEditor.Models.Services.Hierarchies;

public class Hierarchy
{
	public class Node
	{
		public GameEntity Entity { get; }

		public Node(GameEntity entity)
		{
			Entity = entity;
		}
	}
	
	public string Name { get; }
	public IEnumerable<Node> Nodes => _nodes;

	private readonly List<Node> _nodes = new();

	public Hierarchy(Scene scene)
	{
		Name = scene.Name;

		foreach (var sceneEntity in scene.Entities)
		{
			var node = new Node(sceneEntity);
			_nodes.Add(node);
		}
	}
}