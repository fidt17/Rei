#pragma once

#include "Modules/Render/Material/Material.h"
#include "Modules/Render/Model/Model.h"
#include "Modules/Render/RenderScenario/CameraModule.h"

namespace rei::render
{
    class UIRenderModule
    {
    public:
        explicit UIRenderModule(const std::shared_ptr<CameraModule>& cameraModule);
        ~UIRenderModule();

        void Setup();
        void Render() const;

    private:
        void EnsureQuadModel();
        void HandleUiRenderingEnabledSetEvent(bool value);

    private:
        std::shared_ptr<CameraModule> _cameraModule;
        assets::AssetRef<Model> _quadModel;
        bool _isEnabled = true;

        REI_EVENT_HANDLE(bool) _uiRenderingEnabledSetHandle;
    };
}
