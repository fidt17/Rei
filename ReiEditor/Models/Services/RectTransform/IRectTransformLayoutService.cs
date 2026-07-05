using System.Collections.Generic;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Entities;

namespace ReiEditor.Models.Services.RectTransform;

public interface IRectTransformLayoutService
{
    RectTransformVector2 GetParentSize(GameEntity entity);
    bool TryGetRectTransform(GameEntity entity, out BehaviourComponent rectTransform);
    bool TryReadLayout(BehaviourComponent rectTransform, out RectTransformLayoutData data);
    bool TryPreserveRectForPivot(GameEntity entity, BehaviourComponent rectTransform, float pivotX, float pivotY, out RectTransformLayoutData preservedLayout);
    bool TryPreserveRectForParent(GameEntity entity, GameEntity? newParent, out RectTransformLayoutData preservedLayout);
    void ApplyLayoutToEditor(BehaviourComponent rectTransform, RectTransformLayoutData layout);
    Dictionary<string, object?> SerializeVector2(RectTransformVector2 value);
}
