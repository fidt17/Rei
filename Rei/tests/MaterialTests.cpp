#include "pch.h"
#include "catch_amalgamated.hpp"
#include "Modules/Render/Material/Material.h"

using rei::render::Material;

TEST_CASE("Material numeric reads handle absent and nonnumeric properties", "[material]")
{
    Material material;
    material.REI_SET({{"Properties", {{"integer", -3}, {"float", 2.5}, {"invalid", "value"}, {"null", nullptr}}}});
    REQUIRE(material.GetInt("integer") == -3);
    REQUIRE(material.GetFloat("integer") == -3.0f);
    REQUIRE(material.GetFloat("float") == 2.5f);
    REQUIRE(material.GetInt("float") == 2);
    REQUIRE(material.GetInt("missing", 17) == 17);
    REQUIRE(material.GetFloat("invalid", 0.8f) == 0.8f);
    REQUIRE(material.GetInt("null", 9) == 9);
    REQUIRE(material.GetInt("") == 0);
    REQUIRE_FALSE(material.REI_GET().at("Properties").contains("missing"));
}

TEST_CASE("Material setters stage final values without a graphics context", "[material]")
{
    // A resolved shader must not make property writes depend on GL state.
    rei::assets::AssetRef<rei::render::Shader> shader("unit-shader");
    shader.Record = std::make_shared<rei::assets::AssetRecord>();
    shader.Record->Id = shader.Id;
    shader.Record->State = rei::assets::AssetState::Loaded;
    shader.Record->Value = std::make_shared<rei::render::Shader>();
    Material material(shader);
    material.SetInt("mode", 1);
    material.SetInt("mode", 2);
    material.SetFloat("speed", 0.5f);
    material.SetFloat("speed", 1.5f);
    material.SetColor("color", rei::render::Color(0.25f, 0.5f, 0.75f, 1));
    material.SetTexture("texture", rei::assets::AssetRef<rei::render::Texture>("unloaded-texture"));
    material.SetInt("discard", 3);
    material.ClearProperty("discard");
    material.SetFloat("", 2);
    REQUIRE(material.GetInt("mode") == 2);
    REQUIRE(material.GetFloat("speed") == 1.5f);
    const auto properties = material.REI_GET().at("Properties");
    REQUIRE(properties.at("color").at("b") == 0.75f);
    REQUIRE(properties.at("texture").at("Id") == "unloaded-texture");
    REQUIRE_FALSE(properties.contains("discard"));
    REQUIRE_FALSE(properties.contains(""));
}

TEST_CASE("Material typed reads reflect replacement and clearing immediately", "[material]")
{
    Material material;
    material.SetInt("mode", 2);
    material.REI_SET({{"Properties", {{"mode", 7}, {"delay", 0.8f}}}});
    REQUIRE(material.GetInt("mode") == 7);
    REQUIRE(material.GetFloat("delay") == 0.8f);
    material.ClearProperty("mode");
    REQUIRE(material.GetInt("mode", -1) == -1);
    Material restored;
    restored.REI_SET(material.REI_GET());
    REQUIRE(restored.GetFloat("delay") == 0.8f);
    REQUIRE(restored.GetInt("mode", -1) == -1);
}
