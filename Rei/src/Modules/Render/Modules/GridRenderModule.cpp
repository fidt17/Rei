#include "pch.h"
#include "GridRenderModule.h"

#include "Gizmos.h"
#include "Api/EditorApi.h"
#include "rei_behaviours/transformation/Transform.h"

rei::render::GridRenderModule::GridRenderModule(const std::shared_ptr<CameraModule>& cameraModule, const std::shared_ptr<Gizmos>& gizmos):
    _cameraModule(cameraModule),
    _gizmos(gizmos)
{
    _h = GetEditorEventsRelay().GridRenderSettingsReceivedEvent.append([this](const GridRenderSettings& gridRenderSettings)
    {
        HandleGridRenderSettingsSetEvent(gridRenderSettings);
    });
}

rei::render::GridRenderModule::~GridRenderModule()
{
    GetEditorEventsRelay().GridRenderSettingsReceivedEvent.remove(_h);
}

void rei::render::GridRenderModule::Setup()
{
    _gridMaterial = GetAssetManager().GetById<Material>(REI_EDITOR_GRID_MATERIAL_ID);
}

void rei::render::GridRenderModule::DrawGrids()
{
    constexpr f32 gridSize = 180;
    constexpr f32 cellSize = 2;

    if (_settings.RenderXZ)
    {
        RenderGridAtCameraXZ(gridSize, cellSize);
    }

    if (_settings.RenderXY)
    {
        RenderGridAtCameraXY(gridSize, cellSize);
    }

    if (_settings.RenderYZ)
    {
        RenderGridAtCameraYZ(gridSize, cellSize);
    }
}

void rei::render::GridRenderModule::RenderGridAtCameraXZ(f32 gridSize, f32 cellSize)
{
    const auto& cameraPos = _cameraModule->GetCamera().Get().GetTransform().GetPosition();

    const i32 xOffset = (cameraPos.x / cellSize);
    const i32 zOffset = (cameraPos.z / cellSize);

    const math::Vector3 center(xOffset * cellSize, 0, zOffset * cellSize);

    RenderGrid(gridSize, cellSize, center, {0, 1, 0});
}

void rei::render::GridRenderModule::RenderGridAtCameraXY(f32 gridSize, f32 cellSize)
{
    const auto& cameraPos = _cameraModule->GetCamera().Get().GetTransform().GetPosition();

    const i32 xOffset = (cameraPos.x / cellSize);
    const i32 yOffset = (cameraPos.y / cellSize);

    const math::Vector3 center(xOffset * cellSize, yOffset * cellSize, 0);

    RenderGrid(gridSize, cellSize, center, {0, 0, 1});
}

void rei::render::GridRenderModule::RenderGridAtCameraYZ(f32 gridSize, f32 cellSize)
{
    const auto& cameraPos = _cameraModule->GetCamera().Get().GetTransform().GetPosition();

    const i32 yOffset = (cameraPos.y / cellSize);
    const i32 zOffset = (cameraPos.z / cellSize);

    const math::Vector3 center(0, yOffset * cellSize, zOffset * cellSize);

    RenderGrid(gridSize, cellSize, center, {1, 0, 0});
}

void rei::render::GridRenderModule::RenderGrid(const f32 gridSize, const f32 cellSize, const math::Vector3& center, const math::Vector3& direction)
{
    using math::Vector3;

    std::unique_ptr<GridVertexData> grid;
    auto it = std::ranges::find_if(_grids, [=](const std::unique_ptr<GridVertexData>& g) { return g->GetSize() == gridSize && g->GetCellSize() == cellSize; });
    if (it == _grids.end())
    {
        _grids.emplace_back(std::make_unique<GridVertexData>(gridSize, cellSize));
        it = _grids.end() - 1;
    }

    auto model = glm::mat4(1);
    model = translate(model, glm::vec3(center));
    model = model * LookAt(glm::vec3(0, 0, 0), direction, {0, 1, 0});

    const auto& shader = _gridMaterial.Asset->GetShader();
    shader.SetColor("_Color", {1, 1, 1, _settings.Opacity});
    shader.SetVector3("_CameraPos", _cameraModule->GetCamera().Get().GetTransform().GetPosition());
    shader.SetViewMatrices(_cameraModule->GetProjectionMatrix(), _cameraModule->GetViewMatrix(), model);

    glEnable(GL_DEPTH_TEST);
    it->get()->Render();
}

void rei::render::GridRenderModule::HandleGridRenderSettingsSetEvent(const GridRenderSettings& settings)
{
    _settings = settings;
}
