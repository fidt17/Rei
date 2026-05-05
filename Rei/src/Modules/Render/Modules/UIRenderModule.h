#pragma once

#include "Modules/Render/Material/Material.h"
#include "Modules/Render/Model/Model.h"
#include "Modules/Render/RenderScenario/CameraModule.h"

namespace rei::render
{
    class UIRenderModule
    {
        enum class UiRenderItemType
        {
            Image,
            Text
        };

        struct UiRenderItem
        {
            ecs::Entity Entity;
            UiRenderItemType Type;
        };

    public:
        explicit UIRenderModule(const std::shared_ptr<CameraModule>& cameraModule);
        ~UIRenderModule();

        void Setup();
        void Render() const;

    private:
        void EnsureQuadModel();
        void EnsureTextRenderObjects();
        void DisposeTextRenderObjects();
        std::vector<UiRenderItem> CollectUiRenderItems() const;
        void CollectUiRenderItems(ecs::Entity entity, std::vector<UiRenderItem>& renderItems) const;
        void RenderUiItems(const std::vector<UiRenderItem>& renderItems, const glm::mat4& projection, const glm::mat4& view) const;
        void DrawImage(ecs::Entity entity, const glm::mat4& projection, const glm::mat4& view) const;
        void DrawUiText(ecs::Entity entity, const glm::mat4& projection, const glm::mat4& view) const;
        void DrawGlyphQuad(f32 x, f32 y, f32 width, f32 height, u32 textureId) const;
        void HandleUiRenderingEnabledSetEvent(bool value);

    private:
        std::shared_ptr<CameraModule> _cameraModule;
        assets::AssetRef<Model> _quadModel;
        assets::AssetRef<Shader> _textShader;
        u32 _textVao = 0;
        u32 _textVbo = 0;
        bool _isEnabled = true;

        REI_EVENT_HANDLE(bool) _uiRenderingEnabledSetHandle;
    };
}
