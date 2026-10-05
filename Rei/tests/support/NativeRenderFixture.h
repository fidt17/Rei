#pragma once
#include "NativeGlFixture.h"
#include "AssetTestSupport.h"
#include "Common/Diagnostics/DiagnosticsService.h"
#include "Api/EditorEventsRelay.h"
#include "Modules/Render/Shaders/ShaderGenerator.h"

namespace rei::tests
{
    inline std::vector<u8> RenderTextBytes(const std::string& text)
    {
        std::vector<u8> bytes;
        AppendString(bytes, text);
        return bytes;
    }

    inline std::vector<u8> RenderTextureBytes(const std::vector<u8>& pixels)
    {
        std::vector<u8> bytes;
        for (const auto value : {1, 1, static_cast<i32>(GL_RGBA), static_cast<i32>(pixels.size())}) AppendInteger(bytes, value, 4);
        bytes.insert(bytes.end(), pixels.begin(), pixels.end());
        return bytes;
    }

    inline void PrepareRenderResources(TemporaryDirectory& files)
    {
        // Copy only the checked-in shader text; never read or mutate a user's pack.
        const auto resources = std::filesystem::absolute(std::filesystem::path(__FILE__)).parent_path().parent_path().parent_path() / "resources";
        const auto source = [&](const std::string& path)
        {
            const auto file = resources / path;
            if (!std::filesystem::is_regular_file(file)) throw std::runtime_error("Render prerequisite unavailable: shader source " + file.string());
            return RenderTextBytes(ReadText(file));
        };
        std::vector<PackedAsset> pack;
        for (const auto& [id, path] : std::vector<std::pair<std::string, std::string>>{
            {REI_SHADER_INCLUDE_AMBIENT_LIGHT_ASSET_ID, "shaders/includes/include_ambient_light.rshader_include"},
            {REI_SHADER_INCLUDE_POINT_LIGHT_ASSET_ID, "shaders/includes/include_point_light.rshader_include"},
            {REI_SHADER_INCLUDE_SHADER_COMMON_ASSET_ID, "shaders/includes/include_shader_common.rshader_include"},
            {REI_SHADER_INCLUDE_VERTEX_COMMON_ASSET_ID, "shaders/includes/include_vertex_common.rshader_include"},
            {REI_SHADER_INCLUDE_FRAGMENT_COMMON_ASSET_ID, "shaders/includes/include_fragment_common.rshader_include"},
            {REI_SHADER_COLOR_ASSET_ID, "shaders/standard/color.rshader"},
            {REI_SHADER_TEXT_ASSET_ID, "shaders/standard/text.rshader"},
            {REI_SHADER_SIMPLE_LIT_ASSET_ID, "shaders/standard/simple_lit.rshader"},
            {REI_SHADER_SPRITE_ASSET_ID, "shaders/standard/sprite.rshader"},
            {REI_SHADER_ERROR_ASSET_ID, "shaders/special/error.rshader"},
            {REI_SHADER_LIGHT_SOURCE_ASSET_ID, "shaders/special/light_source.rshader"},
            {REI_SHADER_DEPTH_ASSET_ID, "shaders/special/depth.rshader"},
            {REI_SHADER_ALPHA_OUTLINE_ASSET_ID, "shaders/post_processing/post_processing_alpha_outline.rshader"},
            {REI_SHADER_EDITOR_GRID_ASSET_ID, "shaders/editor/editor_grid.rshader"},
            {REI_SHADER_IMAGE_ASSET_ID, "shaders/standard/image.rshader"},
            {"rei_image.rshader", "shaders/standard/image.rshader"},
            {REI_SHADER_OVERLAY_TEXTURE_ASSET_ID, "shaders/post_processing/post_processing_overlay_texture.rshader"},
            {REI_SHADER_GRAYSCALE_ASSET_ID, "shaders/post_processing/post_processing_grayscale.rshader"},
            {REI_SHADER_INVERSION_ASSET_ID, "shaders/post_processing/post_processing_inversion.rshader"}
        }) pack.push_back({id, id, source(path)});
        for (const auto& [id, shader] : std::vector<std::pair<std::string, std::string>>{
            {REI_IMAGE_MATERIAL_ID, "rei_image.rshader"}, {REI_COLOR_MATERIAL_ID, REI_SHADER_COLOR_ASSET_ID},
            {REI_OVERLAY_TEXTURE_MATERIAL_ID, REI_SHADER_OVERLAY_TEXTURE_ASSET_ID},
            {REI_OVERLAY_GRAYSCALE_MATERIAL_ID, REI_SHADER_GRAYSCALE_ASSET_ID},
            {REI_OVERLAY_INVERSION_MATERIAL_ID, REI_SHADER_INVERSION_ASSET_ID}
        }) pack.push_back({id, id, JsonAsset({{"ShaderAssetId", shader}, {"UseDepth", false}, {"Properties", nlohmann::json::object()}})});
        pack.push_back({REI_WHITE_FALLBACK_TEXTURE_ID, "white", RenderTextureBytes({255, 255, 255, 255})});
        const auto font = ReadBytes(resources / "fonts/Roboto-Regular.ttf");
        if (font.empty()) throw std::runtime_error("Render prerequisite unavailable: Roboto font source");
        std::vector<u8> fontBytes;
        AppendInteger(fontBytes, font.size(), 4);
        fontBytes.insert(fontBytes.end(), font.begin(), font.end());
        pack.push_back({"rei_roboto-regular.ttf", "Roboto", std::move(fontBytes)});
        pack.push_back({"0", "build-scenes", JsonAsset({{"Scenes", {{"0", "render-empty-scene"}}}})});
        pack.push_back({"render-empty-scene", "scene", JsonAsset({{"Name", "native render fixture"}, {"Entities", nlohmann::json::array()}})});
        WriteAssetPack(files, pack);
    }

    class NativeRenderFixture
    {
    public:
        NativeGlFixture Gl;
        BehaviourFixture Scene;
        std::shared_ptr<api::EditorEventsRelay> Relay = std::make_shared<api::EditorEventsRelay>();
        std::shared_ptr<common::diagnostics::DiagnosticsService> Diagnostics = std::make_shared<common::diagnostics::DiagnosticsService>();

        NativeRenderFixture() : Scene(PrepareRenderResources)
        {
            Services::GetInstance()->SetEditorEventsRelay(Relay);
            Services::GetInstance()->SetDiagnostics(Diagnostics);
            render::ShaderGenerator::GetInstance().Initialize();
        }

        ~NativeRenderFixture()
        {
            Services::GetInstance()->SetGizmos(nullptr);
            Services::GetInstance()->SetEditorEventsRelay(nullptr);
            Services::GetInstance()->SetDiagnostics(nullptr);
        }
    };
}
