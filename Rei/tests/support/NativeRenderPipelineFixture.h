#pragma once
#include "NativeRenderFixture.h"
#include "NativeEngineFixture.h"
#include "Modules/Components/ActiveTag.h"
#include "rei_behaviours/render/MeshRenderer.h"
#include "rei_behaviours/render/light/PointLight.h"
#include "rei_behaviours/ui/Canvas.h"
#include "rei_behaviours/ui/RectTransform.h"
#include "rei_behaviours/ui/Image.h"
#include <condition_variable>

namespace rei::tests
{
    struct CapturedFrame
    {
        std::mutex Mutex;
        std::condition_variable Changed;
        bool Done = false;
        bool Accepted = false;
        i32 Width = 0;
        i32 Height = 0;
        std::vector<u8> Pixels;
    };

    inline std::shared_ptr<CapturedFrame> Capture(NativeEngineFixture& engine)
    {
        auto frame = std::make_shared<CapturedFrame>();
        engine.OnEngineThread([&engine, frame]
        {
            frame->Accepted = engine.Engine->RequestFrameCapture([frame](const u8* pixels, const i32 width, const i32 height)
            {
                std::scoped_lock lock(frame->Mutex);
                frame->Width = width;
                frame->Height = height;
                if (pixels) frame->Pixels.assign(pixels, pixels + static_cast<size_t>(width) * height * 4);
                frame->Done = true;
                frame->Changed.notify_all();
            });
        });
        if (!frame->Accepted) throw std::runtime_error("Real renderer rejected frame capture");
        std::unique_lock lock(frame->Mutex);
        if (!frame->Changed.wait_for(lock, std::chrono::seconds(5), [&] { return frame->Done; })) throw std::runtime_error("Real renderer frame capture timeout");
        return frame;
    }

    inline std::array<u8, 4> Pixel(const CapturedFrame& frame, const i32 x, const i32 y)
    {
        REQUIRE(x >= 0);
        REQUIRE(y >= 0);
        REQUIRE(x < frame.Width);
        REQUIRE(y < frame.Height);
        const auto index = (static_cast<size_t>(y) * frame.Width + x) * 4;
        REQUIRE(index + 4 <= frame.Pixels.size());
        return {frame.Pixels[index], frame.Pixels[index + 1], frame.Pixels[index + 2], frame.Pixels[index + 3]};
    }

    inline ecs::Entity CreateEntity(const std::string& name)
    {
        const auto entity = GetEntityManager().CreateNewEntity(name);
        GetInternalWorld()->GetRegistry()->Get<ActiveTag>(entity);
        return entity;
    }

    inline ecs::Entity CreateCamera()
    {
        const auto entity = CreateEntity("pipeline camera");
        GetEntityManager().AddBehaviour(entity, 7301, nlohmann::json(), false);
        auto& camera = GetInternalWorld()->GetRegistry()->Get<render::Camera>(entity);
        camera.SetPerspective(render::Orthographic);
        camera.REI_SET(SerializedField("_backgroundColor", {{"r", { {"Value", 0} }}, {"g", { {"Value", 0} }}, {"b", { {"Value", 0} }}, {"a", { {"Value", 1} }}}));
        return entity;
    }

    inline ecs::Entity CreateMesh(const f32 z, const render::Color& color)
    {
        const auto entity = CreateEntity("pipeline mesh");
        GetEntityManager().AddBehaviour(entity, 7308, nlohmann::json(), false);
        auto registry = GetInternalWorld()->GetRegistry();
        auto& mesh = registry->Get<render::MeshRenderer>(entity);
        const std::vector<render::Vertex> vertices = {
            {{-2, -2, z}, {0, 0, -1}, {0, 0}}, {{2, -2, z}, {0, 0, -1}, {1, 0}},
            {{2, 2, z}, {0, 0, -1}, {1, 1}}, {{-2, 2, z}, {0, 0, -1}, {0, 1}}
        };
        auto model = GetAssetManager().CreateAsset<render::Model>("pipeline quad", render::Mesh("quad", vertices, {0, 1, 2, 0, 2, 3}, {}));
        auto material = GetAssetManager().CreateAsset<render::Material>(GetAssetManager().GetById<render::Shader>(REI_SHADER_COLOR_ASSET_ID));
        material->SetColor("_Color", color);
        material->SetDepth(true);
        mesh.SetModel(model);
        mesh.SetMaterial(material);
        return entity;
    }

    inline ecs::Entity CreateCanvas()
    {
        const auto entity = CreateEntity("pipeline canvas");
        GetEntityManager().AddBehaviour(entity, 7302, nlohmann::json(), false);
        GetInternalWorld()->GetRegistry()->Get<ui::Canvas>(entity).REI_SET(SerializedField("_scaleMode", static_cast<i32>(ui::ConstantPixelSize)));
        return entity;
    }

    inline void CreateLitMeshWithLights(const i32 count)
    {
        CreateCamera();
        const auto meshEntity = CreateMesh(2, render::Color::White());
        auto material = GetAssetManager().CreateAsset<render::Material>(GetAssetManager().GetById<render::Shader>(REI_SHADER_SIMPLE_LIT_ASSET_ID));
        material->SetColor("_Color", render::Color::White());
        material->SetFloat("_Shininess", 0);
        GetInternalWorld()->GetRegistry()->Get<render::MeshRenderer>(meshEntity).SetMaterial(material);
        for (i32 i = 0; i < count; ++i)
        {
            const auto lightEntity = CreateEntity("pipeline light");
            auto registry = GetInternalWorld()->GetRegistry();
            registry->Get<Transform>(lightEntity).GetLocalPosition() = {3, 0, 1}; // Native light-marker cubes must not occlude sampled center pixel.
            GetEntityManager().AddBehaviour(lightEntity, 7306, nlohmann::json(), false);
            auto& light = registry->Get<render::PointLight>(lightEntity);
            light.SetColor(render::Color::White());
            light.SetStrength(0.05f);
        }
    }

    inline ecs::Entity CreateImage(const ecs::Entity canvas, const render::Color& color, const math::Vector2& size, const math::Vector2& position)
    {
        const auto entity = CreateEntity("pipeline image");
        auto registry = GetInternalWorld()->GetRegistry();
        registry->Get<Transform>(entity).SetParent(canvas);
        GetEntityManager().AddBehaviour(entity, 7304, nlohmann::json(), false);
        auto& rect = registry->Get<ui::RectTransform>(entity);
        rect.GetSizeDelta() = size;
        rect.GetAnchoredPosition() = position;
        auto& image = registry->Get<ui::Image>(entity);
        image.Init();
        image.SetColor(color);
        return entity;
    }

    inline void Refresh() { GetInternalWorld()->Refresh(); GetInternalWorld()->RefreshAll(); }
}
