using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Dialogs.Internal;
using Newtonsoft.Json;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Entities;
using SkiaSharp;

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
        entity.Transform._order = _entities.Where(x => !x.Transform.HasParent()).Max(x => x.Transform._order) + 1;
    }

    public void DeleteEntity(GameEntity entity)
    {
        _entities.Remove(entity);
        ShiftOrderOfSameParentElementsWithGreaterOrder(entity, entity.Transform._order, -1);
    }

    private void ShiftOrderOfSameParentElementsWithGreaterOrder(GameEntity entity, int order, int shift)
    {
        if (entity.Transform.HasParent())
        {
            var parent = GetById(entity.Transform._parent);
            if (parent == null) return;
            foreach (var child in parent.Transform._children)
            {
                var e = GetById(child);
                if (e == null) continue;
                if (e.Transform._order < order) continue;

                e.Transform._order += shift;
            }
        }
        else
        {
            var entitiesWithGreaterIds = _entities.Where(x => !x.Transform.HasParent() && x.Transform._order >= order).ToList();
            foreach (var gameEntity in entitiesWithGreaterIds)
            {
                gameEntity.Transform._order += shift;
            }
        }
    }

    public bool MoveEntity(GameEntity entity, GameEntity? newParent, int order)
    {
        if (!_entities.Contains(entity)) throw new Exception($"{entity} does not belong to the scene {Name}");
        if (newParent != null && entity.Equals(newParent)) return false;
        if (newParent != null && IsIndirectOrDirectChild(newParent, entity)) return false;

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
            newParent.Transform._children.Add(entity.Id);
            entity.Transform._parent = newParent.Id;
        }
        
        ShiftOrderOfSameParentElementsWithGreaterOrder(entity, order, 1);
        entity.Transform._order = order;

        return true;
    }

    private void RemoveFromParent(GameEntity entity)
    {
        ShiftOrderOfSameParentElementsWithGreaterOrder(entity, entity.Transform._order, -1);
        
        if (entity.Transform.HasParent())
        {
            var currentParent = GetById(entity.Transform._parent);
            currentParent?.Transform._children.Remove(entity.Id);
        }

        entity.Transform._parent = 0;
    }

    private bool IsIndirectOrDirectChild(GameEntity e, GameEntity parent)
    {
        foreach (var transformChild in parent.Transform._children)
        {
            if (transformChild == e.Id) return true;
            
            var child = GetById(transformChild);
            if (child == null) throw new NullReferenceException();
            if (IsIndirectOrDirectChild(e, child)) return true;
        }

        return false;
    }
}