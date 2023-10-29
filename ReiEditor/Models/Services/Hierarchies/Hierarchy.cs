using System;
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

	public event Action<Hierarchy>? ChangedEvent;

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

	public void AddNode(Node node)
	{
		_nodes.Add(node);
		ChangedEvent?.Invoke(this);
	}

	public void RemoveNodeWhere(Func<Node, bool> filter)
	{
		bool didChange = false;
		
		for (var i = _nodes.Count - 1; i >= 0; i--)
		{
			if (!filter(_nodes[i])) continue;
			
			_nodes.RemoveAt(i);
			didChange = true;
		}

		if (didChange)
		{
			ChangedEvent?.Invoke(this);
		}
	}
}