#pragma once
#include "Modules/Render/Material/Material.h"
#include "Modules/Render/Mesh/VertexObjects/GridVertexData.h"
#include "Modules/Render/RenderScenario/CameraModule.h"

namespace rei::render
{
    struct GridRenderSettings
    {
        bool RenderXZ = true;
        bool RenderXY = false;
        bool RenderYZ = false;
        float Opacity = 0.25f;
    };
    
    class GridRenderModule
    {
    public:
        GridRenderModule(const std::shared_ptr<CameraModule>& cameraModule, const std::shared_ptr<Gizmos>& gizmos);
        ~GridRenderModule();

        void Setup();
        void DrawGrids();

    private:
        void RenderGridAtCameraXZ(f32 gridSize, f32 cellSize);
        void RenderGridAtCameraXY(f32 gridSize, f32 cellSize);
        void RenderGridAtCameraYZ(f32 gridSize, f32 cellSize);
        
        void RenderGrid(f32 gridSize, f32 cellSize, const math::Vector3& center, const math::Vector3& direction);

        void HandleGridRenderSettingsSetEvent(const GridRenderSettings& settings);

    private:
        std::shared_ptr<CameraModule> _cameraModule;
        std::shared_ptr<Gizmos> _gizmos;

        GridRenderSettings _settings;
        
        assets::AssetRef<Material> _gridMaterial{};

        std::vector<std::unique_ptr<GridVertexData>> _grids {};

        REI_EVENT_HANDLE(const GridRenderSettings&) _h;
    };
}
