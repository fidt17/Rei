#pragma once
#include <vector>

#include "glm/detail/type_quat.hpp"

namespace rei
{
    class Transform : public Behaviour
    {
    private:
        BEHAVIOUR_BODY(Transform)
        SERIALIZE math::Vector3 _position;
        SERIALIZE math::Vector3 _rotation;
        SERIALIZE math::Vector3 _scale;
        HIDE_IN_EDITOR SERIALIZE i32 _parent = 0;
        HIDE_IN_EDITOR SERIALIZE i32 _order = 0;

        glm::quat _quaternion;
        ecs::Entity _parentEntity = ecs::NULL_ENTITY;

    public:
        REI_API void Reset();

        REI_API void AfterREI_SET() override;
        REI_API void BeforeREI_GET() override;

        REI_API math::Vector3& GetPosition();
        REI_API math::Vector3& GetScale();
        REI_API const glm::quat& GetRotation() const;
        REI_API math::Vector3 GetWorldPosition() const;
        REI_API math::Vector3 GetWorldScale() const;
        REI_API glm::quat GetWorldRotation() const;

        REI_API ecs::Entity GetParent() const;
        REI_API void SetParent(ecs::Entity parent);
        REI_API void SetParent(ecs::Entity parent, i32 order);

        REI_API i32 GetChildOrder() const;
        REI_API void SetChildOrder(i32 order);

        REI_API std::vector<ecs::Entity> GetChildren() const;
        REI_API i32 GetMaxChildOrder() const;

        REI_API void Translate(const math::Vector3& translation);

        REI_API void SetRotation(const math::Vector3& eulerAngles);
        REI_API void SetRotation(const glm::quat& quaternion);

        REI_API void RotateWorld(f32 angle, const math::Vector3& axis);
        REI_API void RotateLocal(f32 angle, const math::Vector3& axis);

        REI_API glm::mat4 CalculateModelMatrix() const;
        REI_API glm::mat4 CalculateWorldModelMatrix() const;

        REI_API math::Vector3 GetForward() const;
        REI_API math::Vector3 GetRight() const;
        REI_API math::Vector3 GetUp() const;

        REI_API operator std::string() const;
    };
}

EXPORT_COMPONENT(rei::Transform)
