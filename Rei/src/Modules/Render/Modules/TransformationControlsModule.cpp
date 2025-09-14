#include "pch.h"
#include "TransformationControlsModule.h"

#include "rei_behaviours/transformation/Transform.h"

rei::render::TransformationControlsModule::TransformationControlsModule(const std::shared_ptr<CameraModule>& cameraModule): _cameraModule(cameraModule)
{
}

void rei::render::TransformationControlsModule::Setup()
{
    _arrowMaterial = GetAssetManager().GetById<Material>(REI_COLOR_MATERIAL_ID);
}

void rei::render::TransformationControlsModule::DrawControls()
{
    ECS_WORLD(rei::GetInternalWorld());
    const auto f = GetInternalWorld().GetFiltersRegistry()->Get<editor::SelectedTag>();

    FOR(e, f)
    {
        DrawMoveControls(GET(e, transformation::Transform));
        break;
    }
}

void rei::render::TransformationControlsModule::DrawMoveControls(transformation::Transform& transform) const
{
    const auto& pos = transform.GetPosition();
    DrawXArrow(pos);
    DrawZArrow(pos);
    DrawYArrow(pos);
}

void rei::render::TransformationControlsModule::DrawXArrow(const math::Vector3& pos) const
{
    DrawArrow(pos, math::Vector3(1.0, 0.0, 0.0), Color::FromHex("#bf212f"));
}

void rei::render::TransformationControlsModule::DrawYArrow(const math::Vector3& pos) const
{
    DrawArrow(pos, math::Vector3(0.0, 1.0, 0.0), Color::FromHex("#27b376"));
}

void rei::render::TransformationControlsModule::DrawZArrow(const math::Vector3& pos) const
{
    DrawArrow(pos, math::Vector3(0.0, 0.0, 1.0), Color::FromHex("#264b96"));
}

void rei::render::TransformationControlsModule::DrawArrow(const math::Vector3& pos, const math::Vector3& dir, const Color& color) const
{
    const auto& cameraPos = _cameraModule->GetCamera().Get().GetTransform().GetPosition();
    const auto distance = math::Vector3::Distance(cameraPos, pos);
    constexpr f32 SCALE_FACTOR = 30;
    const f32 s = distance / SCALE_FACTOR;

    auto model = glm::mat4(1);
    model = translate(model, glm::vec3(pos));
    model = scale(model, glm::vec3(s, s, s));
    model = model * LookAt(glm::vec3(0, 0, 0), dir, glm::vec3(0, 1, 0));

    const auto& shader = _arrowMaterial.Asset->GetShader();
    shader.SetColor("_Color", color);
    shader.SetViewMatrices(_cameraModule->GetProjectionMatrix(), _cameraModule->GetViewMatrix(), model);

    glDisable(GL_DEPTH_TEST);
    _arrow.Render();
}
