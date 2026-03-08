#include "pch.h"

#include "Transform.h"
#include "TransformHierarchyUtility.h"

#include "glm/gtc/quaternion.hpp"
#include "glm/ext/quaternion_trigonometric.hpp"
#include "Modules/Components/EntityInfo.h"
#include "Modules/EntityManagement/EntityManager.h"

namespace rei
{
    void Transform::Reset()
    {
        _position = math::Vector3(0, 0, 0);
        _rotation = math::Vector3(0, 0, 0);
        _scale = math::Vector3(1, 1, 1);
        _parent = 0;
        _order = 0;
        _parentEntity = ecs::NULL_ENTITY;
    }

    void Transform::AfterREI_SET()
    {
        _quaternion = GetQuaternion(_rotation);

        ECS_WORLD(GetInternalWorld())
        if (_parent == 0)
        {
            _parentEntity = ecs::NULL_ENTITY;
        }
        else
        {
            _parentEntity = GetEntityManager().GetBySceneId(_parent);
            if (_parentEntity == GetEntity())
            {
                _parentEntity = ecs::NULL_ENTITY;
                _parent = 0;
            }
            else if (IS_DEAD(_parentEntity) || !HAS(_parentEntity, EntityInfo))
            {
                _parentEntity = ecs::NULL_ENTITY;
            }
        }
    }

    void Transform::BeforeREI_GET()
    {
        _rotation = math::GetEulerAngles(_quaternion, _rotation);

        ECS_WORLD(GetInternalWorld())
        if (IS_DEAD(_parentEntity) || !HAS(_parentEntity, EntityInfo))
        {
            _parent = 0;
        }
        else
        {
            _parent = GET(_parentEntity, EntityInfo).Id;
        }
    }

    math::Vector3& Transform::GetPosition()
    {
        return _position;
    }

    math::Vector3& Transform::GetScale()
    {
        return _scale;
    }

    const glm::quat& Transform::GetRotation() const
    {
        return _quaternion;
    }

    math::Vector3 Transform::GetWorldPosition() const
    {
        const auto worldMatrix = CalculateWorldModelMatrix();
        return math::Vector3(worldMatrix[3]);
    }

    math::Vector3 Transform::GetWorldScale() const
    {
        const auto worldMatrix = CalculateWorldModelMatrix();

        const glm::vec3 right(worldMatrix[0]);
        const glm::vec3 up(worldMatrix[1]);
        const glm::vec3 forward(worldMatrix[2]);

        return {
            glm::length(right),
            glm::length(up),
            glm::length(forward)
        };
    }

    glm::quat Transform::GetWorldRotation() const
    {
        const auto worldMatrix = CalculateWorldModelMatrix();
        const auto worldScale = GetWorldScale();

        glm::mat3 rotationMatrix;
        rotationMatrix[0] = worldScale.x == 0 ? glm::vec3(1, 0, 0) : glm::vec3(worldMatrix[0]) / worldScale.x;
        rotationMatrix[1] = worldScale.y == 0 ? glm::vec3(0, 1, 0) : glm::vec3(worldMatrix[1]) / worldScale.y;
        rotationMatrix[2] = worldScale.z == 0 ? glm::vec3(0, 0, 1) : glm::vec3(worldMatrix[2]) / worldScale.z;

        return glm::normalize(glm::quat_cast(rotationMatrix));
    }

    ecs::Entity Transform::GetParent() const
    {
        return _parentEntity;
    }

    i32 Transform::GetChildOrder() const
    {
        return _order;
    }

    std::vector<ecs::Entity> Transform::GetChildren() const
    {
        ECS_WORLD(GetInternalWorld())

        const auto& entityInfoFilter = FILTER(EntityInfo);
        std::vector<ecs::Entity> children;

        FOR(child, entityInfoFilter)
        {
            if (IS_DEAD(child) || !HAS(child, Transform)) continue;

            const auto& childTransform = GET(child, Transform);

            if (childTransform.GetParent() != GetEntity()) continue;

            children.push_back(child);
        }

        std::ranges::sort(children, [&](const ecs::Entity& a, const ecs::Entity& b)
        {
            const auto& aTransform = GET(a, Transform);
            const auto& bTransform = GET(b, Transform);
            return aTransform.GetChildOrder() < bTransform.GetChildOrder();
        });

        return children;
    }

    void Transform::Translate(const math::Vector3& translation)
    {
        _position += translation;
    }

    void Transform::SetRotation(const math::Vector3& eulerAngles)
    {
        SetRotation(GetQuaternion(eulerAngles));
    }

    void Transform::SetRotation(const glm::quat& quaternion)
    {
        _quaternion = quaternion;
        _rotation = math::GetEulerAngles(_quaternion, _rotation);
    }

    void Transform::SetParent(const ecs::Entity parent)
    {
        if (parent == GetEntity()) return;

        ECS_WORLD(GetInternalWorld())

        _parentEntity = parent;

        if (IS_DEAD(parent) || !HAS(parent, EntityInfo))
        {
            _parent = 0;
        }
        else
        {
            _parent = GET(parent, EntityInfo).Id;
        }
    }

    void Transform::SetParent(const ecs::Entity parent, const i32 order)
    {
        transform_utility::MoveWithOrder(*this, parent, order);
    }

    void Transform::SetChildOrder(const i32 order)
    {
        _order = order;
    }

    i32 Transform::GetMaxChildOrder() const
    {
        ECS_WORLD(GetInternalWorld())

        i32 maxOrder = -1;
        const auto& entityInfoFilter = FILTER(EntityInfo);
        FOR(e, entityInfoFilter)
        {
            if (IS_DEAD(e) || !HAS(e, Transform) || !HAS(e, EntityInfo)) continue;

            const auto& transform = GET(e, Transform);
            if (transform.GetParent() != GetEntity()) continue;

            maxOrder = std::max(maxOrder, transform.GetChildOrder());
        }

        return maxOrder;
    }

    void Transform::RotateWorld(f32 angle, const math::Vector3& axis)
    {
        const f32 angleRad = glm::radians(angle);

        const glm::quat delta = angleAxis(angleRad, glm::vec3(math::Vector3::Normalize(axis)));

        _quaternion = delta * _quaternion;

        _quaternion = normalize(_quaternion);
        _rotation = math::GetEulerAngles(_quaternion, _rotation);
    }

    void Transform::RotateLocal(const f32 angle, const math::Vector3& axis)
    {
        const f32 angleRad = glm::radians(angle);

        const glm::quat delta = angleAxis(angleRad, glm::vec3(math::Vector3::Normalize(axis)));

        _quaternion = _quaternion * delta;

        _quaternion = normalize(_quaternion);
        _rotation = math::GetEulerAngles(_quaternion, _rotation);
    }

    glm::mat4 Transform::CalculateModelMatrix() const
    {
        return GetTransformationMatrix(_position, _quaternion, _scale);
    }

    glm::mat4 Transform::CalculateWorldModelMatrix() const
    {
        ECS_WORLD(GetInternalWorld())

        if (IS_DEAD(_parentEntity) || !HAS(_parentEntity, Transform))
        {
            return CalculateModelMatrix();
        }

        const auto& parentTransform = GET(_parentEntity, Transform);
        return parentTransform.CalculateWorldModelMatrix() * CalculateModelMatrix();
    }

    math::Vector3 Transform::GetForward() const
    {
        return _quaternion * glm::vec3(0, 0, 1);
    }

    math::Vector3 Transform::GetRight() const
    {
        return _quaternion * glm::vec3(1, 0, 0);
    }

    math::Vector3 Transform::GetUp() const
    {
        return _quaternion * glm::vec3(0, 1, 0);
    }

    Transform::operator std::string() const
    {
        auto f = std::format("P: {}\nR: {}\nS: {}", std::string(_position), std::string(_rotation), std::string(_scale));
        return std::string(f);
    }
}
