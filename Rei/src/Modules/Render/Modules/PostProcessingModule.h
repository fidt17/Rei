#pragma once
#include "Modules/Render/Material/Material.h"
#include "Modules/Render/Mesh/VertexObjects/QuadVertexData.h"
#include "Modules/Render/RenderScenario/CameraModule.h"
#include "Modules/Render/RenderScenario/FrameBuffer.h"

namespace rei::render
{
    class PostProcessingModule
    {
    public:
        explicit PostProcessingModule(const std::shared_ptr<CameraModule>& cameraModule);

        void Setup();
        void OnBeforeRender();
        void Render(const FrameBuffer& frameBuffer) const;

    private:
        struct UniformState
        {
            u64 ProgramRevision = 0;
            f32 ExposureEV = 0;
            ToneMappingMode ToneMapping = Off;
        };

        struct OutputState
        {
            assets::AssetRef<Material> Material;
            UniformState Uniforms;
        };

        OutputState& GetOutput(RenderMode renderMode);
        static void UpdateUniforms(const Shader& shader, const Camera& camera, UniformState& state);
        void DrawOutput(const FrameBuffer& frameBuffer) const;

        OutputState _overlayOutput;
        OutputState _grayscaleOutput;
        OutputState _inversionOutput;
        OutputState* _activeOutput = nullptr;
        std::shared_ptr<CameraModule> _cameraModule;

        QuadVertexData _quadVertexData;
    };
}
