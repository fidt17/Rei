#include "pch.h"
#include "Gizmos.h"

#include "glad/glad.h"
#include "meshes/CircleVertexData.h"
#include "Modules/Behaviour/Components/BehaviourCollection.h"
#include "Modules/EntityManagement/EntityManager.h"
#include "Modules/Render/Material/Material.h"

rei::render::Gizmos::Gizmos(const std::shared_ptr<CameraModule>& cameraModule): _cameraModule(cameraModule)
{
}

void rei::render::Gizmos::Setup()
{
    _gizmosMaterial = GetAssetManager().GetById<Material>(REI_GIZMOS_MATERIAL_ID);
}

void rei::render::Gizmos::RenderBehaviourGizmos()
{
    ECS_WORLD(GetInternalWorld());

    const auto f = GetInternalWorld().GetFiltersRegistry()->Get<BehaviourCollection>();

    FOR(e, f)
    {
        for (const auto behavioursToUpdate : GET(e, BehaviourCollection).Behaviours)
        {
            GetEntityManager().GetBehaviour(e, behavioursToUpdate).DrawGizmos(*this);
        }
    }
}

void rei::render::Gizmos::DrawLine(const math::Vector3& start, const math::Vector3& end, const Color& color, const bool useDepth) const
{
    using glm::vec3;
    using glm::mat4;

    const vec3 direction = vec3(end - start);
    const f32 distance = length(direction) / 1.7320508076;
    const vec3 v1_normalized = normalize(vec3(1, 1, 1));
    const vec3 v2_normalized = normalize(vec3(direction));
    const vec3 rotation_axis = cross(v1_normalized, v2_normalized);
    const f32 dotProduct = dot(v1_normalized, v2_normalized);
    const f32 angle = glm::acos(glm::clamp(dotProduct, -1.0f, 1.0f)); // Clamp to avoid floating point errors with acos
    const mat4 rotation = rotate(mat4(1.0f), angle, rotation_axis);

    auto model = mat4(1.0f);
    model = translate(model, vec3(start));
    model = scale(model, vec3(distance, distance, distance));
    model = model * rotation;

    const auto& shader = _gizmosMaterial.Asset->GetShader();
    shader.SetColor("_Color", color);
    shader.SetViewMatrices(_cameraModule->GetProjectionMatrix(), _cameraModule->GetViewMatrix(), model);

    if (!useDepth)
    {
        glDisable(GL_DEPTH_TEST);
    }
    else
    {
        glEnable(GL_DEPTH_TEST);
    }

    _lineMesh.Render();

    glEnable(GL_DEPTH_TEST);
}

void rei::render::Gizmos::DrawBox(const glm::mat4& transformation, const Color& color, bool useDepth) const
{
    DrawBox(transformation, color, useDepth, false);
}

void rei::render::Gizmos::DrawBox(const math::Vector3& pos, const math::Vector3& size, const math::Vector3& rotation, const Color& color,
                                  const bool useDepth) const
{
    DrawBox(GetTransformationMatrix(pos, rotation, size), color, useDepth, false);
}

void rei::render::Gizmos::DrawWireframeBox(const glm::mat4& transformation, const Color& color, bool useDepth) const
{
    DrawBox(transformation, color, useDepth, true);
}

void rei::render::Gizmos::DrawWireframeBox(const math::Vector3& center, const math::Vector3& size, const math::Vector3& rotation, const Color& color,
                                           const bool useDepth) const
{
    const math::Vector3 halfSize = size / 2.0f;

    std::vector<math::Vector3> vertices = {
        {-halfSize.x, -halfSize.y, +halfSize.z}, // bottom left front
        {+halfSize.x, -halfSize.y, +halfSize.z}, // bottom right front
        {+halfSize.x, +halfSize.y, +halfSize.z}, // top right front
        {-halfSize.x, +halfSize.y, +halfSize.z}, // top left front
        {-halfSize.x, -halfSize.y, -halfSize.z}, // bottom left back
        {+halfSize.x, -halfSize.y, -halfSize.z}, // bottom right back
        {+halfSize.x, +halfSize.y, -halfSize.z}, // top right back
        {-halfSize.x, +halfSize.y, -halfSize.z} // top left back
    };

    const auto rotationMatrix = GetRotationMatrix(rotation);

    for (auto& vertex : vertices)
    {
        vertex = vertex.Transform(rotationMatrix) + center;
    }

    // Front face
    DrawLine(vertices[0], vertices[1], color, useDepth); // bottom
    DrawLine(vertices[1], vertices[2], color, useDepth); // right
    DrawLine(vertices[2], vertices[3], color, useDepth); // top
    DrawLine(vertices[3], vertices[0], color, useDepth); // left

    // Back face
    DrawLine(vertices[4], vertices[5], color, useDepth); // bottom
    DrawLine(vertices[5], vertices[6], color, useDepth); // right
    DrawLine(vertices[6], vertices[7], color, useDepth); // top
    DrawLine(vertices[7], vertices[4], color, useDepth); // left

    // Connecting edges between front and back faces
    DrawLine(vertices[0], vertices[4], color, useDepth); // bottom left
    DrawLine(vertices[1], vertices[5], color, useDepth); // bottom right
    DrawLine(vertices[2], vertices[6], color, useDepth); // top right
    DrawLine(vertices[3], vertices[7], color, useDepth); // top left
}

void rei::render::Gizmos::DrawCircle(const math::Vector3& center, const math::Vector3& forward, const math::Vector3& up, const f32 radius, const Color& color, i32 segments, const bool useDepth)
{
    using math::Vector3;

    segments = std::max(segments, 4);

    auto model = glm::mat4(1);
    model = translate(model, glm::vec3(center));
    model = model * LookAt(glm::vec3(0, 0, 0), glm::vec3(forward), up);
    model = scale(model, {radius, radius, radius});

    const auto& shader = _gizmosMaterial.Asset->GetShader();
    shader.SetColor("_Color", color);
    shader.SetViewMatrices(_cameraModule->GetProjectionMatrix(), _cameraModule->GetViewMatrix(), model);

    if (!useDepth)
    {
        glDisable(GL_DEPTH_TEST);
    }
    else
    {
        glEnable(GL_DEPTH_TEST);
    }

    if (!_circles.contains(segments))
    {
        _circles.emplace(segments, std::make_unique<CircleVertexData>(segments));
    }
    _circles[segments]->Render();

    glEnable(GL_DEPTH_TEST);
}

void rei::render::Gizmos::DrawWireSphere(const math::Vector3& center, const f32 radius, const Color& color, i32 segments, const bool useDepth)
{
    using math::Vector3;

    segments = std::max(segments, 4);

    for (int i = 0; i <= segments; i++)
    {
        const f32 angle = PI * i / segments;
        const f32 circleRadius = radius * sin(angle);
        const f32 offset = radius * cos(angle);

        Vector3 circleCenter = {center.x, center.y + offset, center.z};
        DrawCircle(circleCenter, Vector3::Up(), Vector3::Right(), circleRadius, color, 2 * segments * (circleRadius / radius), useDepth);
    }

    for (int i = 0; i <= segments; i++)
    {
        const f32 angle = PI * i / segments;
        const f32 circleRadius = radius * sin(angle);
        const f32 offset = radius * cos(angle);

        Vector3 circleCenter = {center.x, center.y, center.z + offset};
        DrawCircle(circleCenter, Vector3::Forward(), Vector3::Up(), circleRadius, color, 2 * segments * (circleRadius / radius), useDepth);
    }
}

void rei::render::Gizmos::DrawBox(const glm::mat4& transformation, const Color& color, const bool useDepth, const bool wireframe) const
{
    if (wireframe)
    {
        glPolygonMode(GL_FRONT_AND_BACK, GL_LINE);
    }
    if (!useDepth)
    {
        glDisable(GL_DEPTH_TEST);
    }
    else
    {
        glEnable(GL_DEPTH_TEST);
    }

    const auto& shader = _gizmosMaterial.Asset->GetShader();
    shader.SetColor("_Color", color);
    shader.SetViewMatrices(_cameraModule->GetProjectionMatrix(), _cameraModule->GetViewMatrix(), transformation);

    _cubeMesh.Render();

    glEnable(GL_DEPTH_TEST);
    if (wireframe)
    {
        glPolygonMode(GL_FRONT_AND_BACK, GL_FILL);
    }
}
