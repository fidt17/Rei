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

    public GameEntity? GetById(int id) => _entities.Find(x => x.Id == id);
    
    public void AddEntity(GameEntity entity)
    {
        if (_entities.Exists(x => x.Equals(entity))) throw new Exception($"Entity with Id {entity.Id} already exists in scene");

        _entities.Add(entity);
        entity.Transform._order = _entities.Count - 1;
    }

    public void DeleteEntity(GameEntity entity)
    {
        _entities.Remove(entity);
        UpdateEntitiesTransformOrder();
    }

    public bool MoveEntity(GameEntity entity, GameEntity? newParent, int order)
    {
        if (!_entities.Contains(entity)) throw new Exception($"{entity} does not belong to the scene {Name}");
        if (newParent != null && entity.Equals(newParent)) return false;

        var currentParent = GetById(entity.Transform._parent);
        var currentOrder = entity.Transform._order;
        
        if (currentParent == newParent)
        {
            if (order > currentOrder) order -= 1;
            else if (order == currentOrder) return false;
        }
        
        order = Math.Clamp(order, 0, newParent == null ? _entities.Count - 1 : newParent.Transform._children.Count);
        RemoveFromParent(entity);

        if (newParent != null)
        {
            newParent.Transform._children.Insert(order, entity.Id);
            entity.Transform._parent = newParent.Id;
        }
        else
        {
            var entitiesWithGreaterIds = _entities.Where(x => x.Transform._order >= order).ToList();
            foreach (var gameEntity in entitiesWithGreaterIds)
            {
                gameEntity.Transform._order += 1;
            }
            entity.Transform._order = order;
        }

        UpdateEntitiesTransformOrder();

        return true;
    }

    private void RemoveFromParent(GameEntity entity)
    {
        if (entity.Transform.HasParent())
        {
            var currentParent = GetById(entity.Transform._parent);
            currentParent?.Transform._children.Remove(entity.Id);
        }
        else
        {
            var entitiesWithGreaterIds = _entities.Where(x => x.Transform._order >= entity.Transform._order).ToList();
            foreach (var gameEntity in entitiesWithGreaterIds)
            {
                gameEntity.Transform._order -= 1;
            }
        }
    }

    private void UpdateEntitiesTransformOrder()
    {
        for (var i = 0; i < _entities.Count; i++)
        {
            if (!_entities[i].Transform.HasParent()) continue;
            
            var parent = GetById(_entities[i].Transform._parent);
            if (parent == null) throw new NullReferenceException();
            _entities[i].Transform._order = parent.Transform._children.IndexOf(_entities[i].Id);
        }
    }
    
}