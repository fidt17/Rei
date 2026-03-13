using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using ReiEditor.Models.Services.Assets.Scripting;

namespace ReiEditor.Models.Services.Entities.Sync;

public static class EntityStateSyncUtility
{
    public static bool TryGetTransformData(List<Dictionary<string, object>> behaviours, out int? parent, out int? order)
    {
        parent = null;
        order = null;

        foreach (var behaviourState in behaviours)
        {
            if (!behaviourState.TryGetValue("REI_TYPE", out var reiTypeValue)) continue;

            var reiType = reiTypeValue as string;
            if (string.IsNullOrWhiteSpace(reiType) || reiType != EngineBehavioursConstants.TRANSFORM) continue;

            parent = TryReadInt(behaviourState, EngineBehavioursConstants.TRANSFORM_PARENT);
            order = TryReadInt(behaviourState, EngineBehavioursConstants.TRANSFORM_ORDER);
            return true;
        }

        return false;
    }

    public static int? TryReadInt(Dictionary<string, object> data, string key)
    {
        if (!data.TryGetValue(key, out var value)) return null;

        return value switch
        {
            int intValue => intValue,
            long longValue => (int)longValue,
            JToken token => token.ToObject<int>(),
            _ => null
        };
    }

    // orders entities by hierarchy level and order
    public static List<int> BuildOrderedEntityIds(
        Dictionary<int, int> parentByEntityId,
        Dictionary<int, int> orderByEntityId)
    {
        int CompareByOrderThenId(int leftId, int rightId)
        {
            var orderCompare = orderByEntityId[leftId].CompareTo(orderByEntityId[rightId]);
            return orderCompare != 0 ? orderCompare : leftId.CompareTo(rightId);
        }

        var childrenByParentId = new Dictionary<int, List<int>>();
        foreach (var (entityId, parentId) in parentByEntityId)
        {
            if (!childrenByParentId.TryGetValue(parentId, out var list))
            {
                list = new List<int>();
                childrenByParentId[parentId] = list;
            }

            list.Add(entityId);
        }

        foreach (var childList in childrenByParentId.Values)
        {
            childList.Sort(CompareByOrderThenId);
        }

        var rootEntityIds = new List<int>();
        foreach (var entityId in parentByEntityId.Keys)
        {
            var parentId = parentByEntityId[entityId];
            if (parentId == 0 || !parentByEntityId.ContainsKey(parentId))
            {
                rootEntityIds.Add(entityId);
            }
        }

        rootEntityIds.Sort(CompareByOrderThenId);

        var orderedEntityIds = new List<int>();
        var visitedEntityIds = new HashSet<int>();
        var currentLevelIds = rootEntityIds;

        while (currentLevelIds.Count > 0)
        {
            var nextLevelIds = new List<int>();
            foreach (var entityId in currentLevelIds)
            {
                if (!visitedEntityIds.Add(entityId)) continue;

                orderedEntityIds.Add(entityId);

                if (!childrenByParentId.TryGetValue(entityId, out var children)) continue;
                
                foreach (var childId in children)
                {
                    if (visitedEntityIds.Contains(childId)) continue;
                    
                    nextLevelIds.Add(childId);
                }
            }

            currentLevelIds = nextLevelIds;
        }

        if (visitedEntityIds.Count < parentByEntityId.Count)
        {
            var remainingIds = new List<int>();
            
            foreach (var entityId in parentByEntityId.Keys)
            {
                if (visitedEntityIds.Contains(entityId)) continue;
                
                remainingIds.Add(entityId);
            }

            remainingIds.Sort(CompareByOrderThenId);
            orderedEntityIds.AddRange(remainingIds);
        }

        return orderedEntityIds;
    }
}
