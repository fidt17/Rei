#include "pch.h"
#include "PostProcessingModule.h"

#include "glad/glad.h"
#include "Modules/Render/Material/Material.h"

rei::render::PostProcessingModule::PostProcessingModule(const std::shared_ptr<CameraModule>& cameraModule): _cameraModule(cameraModule)
{
}

void rei::render::PostProcessingModule::Setup()
{
    _overlayMaterial = GetAssetManager().GetById<Material>(REI_OVERLAY_TEXTURE_MATERIAL_ID);
    _grayscaleMaterial = GetAssetManager().GetById<Material>(REI_OVERLAY_GRAYSCALE_MATERIAL_ID);
    _inversionMaterial = GetAssetManager().GetById<Material>(REI_OVERLAY_INVERSION_MATERIAL_ID);
}

void rei::render::PostProcessingModule::Render(const FrameBuffer& frameBuffer) const
{
    const auto renderMode = _cameraModule->GetCamera().Get().GetRenderMode();

    const auto& material = renderMode == Grayscale ? _grayscaleMaterial : renderMode == Inversion ? _inversionMaterial : _overlayMaterial;

    material->GetShader().Use();

    glActiveTexture(GL_TEXTURE0 + 0);
    glBindTexture(GL_TEXTURE_2D, frameBuffer.GetColorTexture());
    _quadVertexData.Render();
}
