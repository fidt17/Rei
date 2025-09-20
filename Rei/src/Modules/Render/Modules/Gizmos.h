#pragma once
#include "Modules/Render/Material/Material.h"
#include "Modules/Render/Mesh/VertexObjects/CircleVertexData.h"
#include "Modules/Render/Mesh/VertexObjects/CubeVertexData.h"
#include "Modules/Render/Mesh/VertexObjects/LineVertexData.h"
#include "Modules/Render/RenderScenario/CameraModule.h"

namespace rei::render
{
    class Gizmos
    {
    public:
        explicit Gizmos(const std::shared_ptr<CameraModule>& cameraModule);

        void Setup();

        void Render();

        REI_API void DrawLine(const math::Vector3& start, const math::Vector3& end, const Color& color, bool useDepth = true) const;

        REI_API void DrawBox(const glm::mat4& transformation, const Color& color, bool useDepth = true) const;
        REI_API void DrawBox(const math::Vector3& pos, const math::Vector3& size, const glm::quat& rotation, const Color& color, bool useDepth = true) const;

        REI_API void DrawWireframeBox(const glm::mat4& transformation, const Color& color, bool useDepth = true) const;
        REI_API void DrawWireframeBox(const math::Vector3& pos, const math::Vector3& size, const glm::quat& rotation, const Color& color, bool useDepth = true) const;

        REI_API void DrawCircle(const math::Vector3& center, const math::Vector3& forward, const math::Vector3& up, f32 radius, const Color& color, i32 segments = 32, bool useDepth = true);
        REI_API void DrawWireSphere(const math::Vector3& center, f32 radius, const Color& color, i32 segments = 32, bool useDepth = true);

        REI_API void EnqueueDrawCommand(const std::function<void(Gizmos& g)>& f);

    private:
        void DrawBox(const glm::mat4& transformation, const Color& color, bool useDepth, bool wireframe) const;

    private:
        std::shared_ptr<CameraModule> _cameraModule;

        std::vector<std::function<void(Gizmos& g)>> _drawCommands;

        assets::AssetRef<Material> _gizmosMaterial{};

        CubeVertexData _cubeMesh;
        LineVertexData _lineMesh;
        std::unordered_map<i32, std::unique_ptr<CircleVertexData>> _circles{};
    };
}
