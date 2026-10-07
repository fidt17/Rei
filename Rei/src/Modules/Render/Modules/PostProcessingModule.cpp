#include "pch.h"
#include "PostProcessingModule.h"
#include "Common/Profiling/ProfileMarkers.h"

#include "glad/glad.h"
#include "Modules/Render/Material/Material.h"

rei::render::PostProcessingModule::PostProcessingModule(const std::shared_ptr<CameraModule>& cameraModule): _cameraModule(cameraModule)
{
}

void rei::render::PostProcessingModule::Setup()
{
    _activeOutput = nullptr;
    _overlayOutput = {GetAssetManager().GetById<Material>(REI_OVERLAY_TEXTURE_MATERIAL_ID), {}};
    _grayscaleOutput = {GetAssetManager().GetById<Material>(REI_OVERLAY_GRAYSCALE_MATERIAL_ID), {}};
    _inversionOutput = {GetAssetManager().GetById<Material>(REI_OVERLAY_INVERSION_MATERIAL_ID), {}};
}

void rei::render::PostProcessingModule::OnBeforeRender()
{
    _activeOutput = nullptr;
    if (!_cameraModule || _cameraModule->GetCamera().IsNull()) return;

    const auto& camera = _cameraModule->GetCamera().Get();
    auto& output = GetOutput(camera.GetRenderMode());
    if (!output.Material.IsLoaded()) return;

    UpdateUniforms(output.Material->GetShader(), camera, output.Uniforms);
    _activeOutput = &output;
}

void rei::render::PostProcessingModule::Render(const FrameBuffer& frameBuffer) const
{
    REI_PROFILE_SCOPE(profiling::markers::OUTPUT.Id);
    if (!_activeOutput || !_activeOutput->Material.IsLoaded()) return;

    _activeOutput->Material->GetShader().Use();
    DrawOutput(frameBuffer);
}

rei::render::PostProcessingModule::OutputState& rei::render::PostProcessingModule::GetOutput(const RenderMode renderMode)
{
    if (renderMode == Grayscale) return _grayscaleOutput;
    if (renderMode == Inversion) return _inversionOutput;
    return _overlayOutput;
}

void rei::render::PostProcessingModule::UpdateUniforms(const Shader& shader, const Camera& camera, UniformState& state)
{
    const auto& settingsRef = camera.GetRendererSettings();
    const auto* settings = settingsRef.IsLoaded() ? settingsRef.Get() : nullptr;
    const f32 exposureEV = settings ? settings->GetExposure() : 0;
    const ToneMappingMode toneMapping = settings ? settings->GetToneMapping() : Off;

    const u64 revision = shader.GetProgramRevision();
    if (state.ProgramRevision != revision || state.ExposureEV != exposureEV)
    {
        shader.SetFloat("_ExposureMultiplier", std::exp2(exposureEV));
        state.ExposureEV = exposureEV;
    }
    if (state.ProgramRevision != revision || state.ToneMapping != toneMapping)
    {
        shader.SetInt("_ToneMapping", static_cast<i32>(toneMapping));
        state.ToneMapping = toneMapping;
    }
    state.ProgramRevision = revision;
}

void rei::render::PostProcessingModule::DrawOutput(const FrameBuffer& frameBuffer) const
{
    glActiveTexture(GL_TEXTURE0 + 0);
    glBindTexture(GL_TEXTURE_2D, frameBuffer.GetColorTexture());
    _quadVertexData.Render();
}
