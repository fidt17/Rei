#include "pch.h"
#include "catch_amalgamated.hpp"
#include "Modules/Render/Material/Material.h"
#include "glad/glad.h"
#include "Common/Profiling/ProfileMarkers.h"
#include <array>
#include <map>

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

// GL spies cover binding/cache semantics; actual rendering is checked in the engine harness.

namespace
{
    template <typename T>
    struct GlFunctionOverride
    {
        T& Slot;
        T Previous;
        GlFunctionOverride(T& slot, T replacement) : Slot(slot), Previous(slot) { Slot = replacement; }
        ~GlFunctionOverride() { Slot = Previous; }
    };

    struct GlBindingSpy
    {
        inline static u32 NextProgram = 1;
        inline static u32 Program = 0;
        inline static i32 LocationQueries = 0;
        inline static i32 ReflectionQueries = 0;
        inline static i32 UniformCount = 0;
        inline static u32 UseCalls = 0;
        inline static u32 ProgramSwitches = 0;
        inline static u32 UniformCalls = 0;
        inline static std::map<std::pair<u32, std::string>, i32> Locations;
        inline static std::map<i32, std::array<f32, 4>> Values;
        inline static std::vector<u32> Deleted;

        GlBindingSpy()
        {
            NextProgram = 1;
            Program = 0;
            LocationQueries = ReflectionQueries = UniformCount = 0;
            UseCalls = ProgramSwitches = UniformCalls = 0;
            Locations.clear();
            Values.clear();
            Deleted.clear();
        }

        GlFunctionOverride<PFNGLCREATESHADERPROC> CreateShader{glad_glCreateShader, +[](GLenum) -> GLuint { return 1; }};
        GlFunctionOverride<PFNGLSHADERSOURCEPROC> ShaderSource{glad_glShaderSource, +[](GLuint, GLsizei, const GLchar* const*, const GLint*) {}};
        GlFunctionOverride<PFNGLCOMPILESHADERPROC> CompileShader{glad_glCompileShader, +[](GLuint) {}};
        GlFunctionOverride<PFNGLGETSHADERIVPROC> GetShaderiv{glad_glGetShaderiv, +[](GLuint, GLenum, GLint* value) { *value = GL_TRUE; }};
        GlFunctionOverride<PFNGLCREATEPROGRAMPROC> CreateProgram{glad_glCreateProgram, +[]() -> GLuint { return NextProgram++; }};
        GlFunctionOverride<PFNGLATTACHSHADERPROC> AttachShader{glad_glAttachShader, +[](GLuint, GLuint) {}};
        GlFunctionOverride<PFNGLLINKPROGRAMPROC> LinkProgram{glad_glLinkProgram, +[](GLuint) {}};
        GlFunctionOverride<PFNGLDELETESHADERPROC> DeleteShader{glad_glDeleteShader, +[](GLuint) {}};
        GlFunctionOverride<PFNGLDELETEPROGRAMPROC> DeleteProgram{glad_glDeleteProgram, +[](GLuint program) { Deleted.push_back(program); }};
        GlFunctionOverride<PFNGLGETPROGRAMIVPROC> GetProgramiv{glad_glGetProgramiv, +[](GLuint, GLenum name, GLint* value)
        {
            if (name == GL_LINK_STATUS) *value = GL_TRUE;
            else if (name == GL_ACTIVE_UNIFORMS) *value = UniformCount;
            else if (name == GL_ACTIVE_UNIFORM_MAX_LENGTH) *value = 16;
        }};
        GlFunctionOverride<PFNGLGETACTIVEUNIFORMPROC> GetActiveUniform{glad_glGetActiveUniform, +[](GLuint, GLuint index, GLsizei, GLsizei* length, GLint* size, GLenum* type, GLchar* name)
        {
            ++ReflectionQueries;
            const std::string uniform = index == 0 ? "tex[0]" : "value";
            *length = static_cast<i32>(uniform.size());
            *size = 1;
            *type = index == 0 ? GL_SAMPLER_2D : GL_FLOAT;
            std::copy(uniform.begin(), uniform.end(), name);
            name[*length] = '\0';
        }};
        GlFunctionOverride<PFNGLGETUNIFORMLOCATIONPROC> GetUniformLocation{glad_glGetUniformLocation, +[](GLuint program, const GLchar* name) -> GLint
        {
            ++LocationQueries;
            if (std::string_view(name) == "absent") return -1;
            const auto key = std::make_pair(program, std::string(name));
            const auto [it, inserted] = Locations.try_emplace(key, static_cast<i32>(Locations.size()) + 1);
            return it->second;
        }};
        GlFunctionOverride<PFNGLUSEPROGRAMPROC> UseProgram{glad_glUseProgram, +[](GLuint program)
        {
            ++UseCalls;
            if (Program != program) ++ProgramSwitches;
            Program = program;
        }};
        GlFunctionOverride<PFNGLUNIFORM1IPROC> Uniform1i{glad_glUniform1i, +[](GLint location, GLint value) { ++UniformCalls; Values[location] = {static_cast<f32>(value), 0, 0, 0}; }};
        GlFunctionOverride<PFNGLUNIFORM1FPROC> Uniform1f{glad_glUniform1f, +[](GLint location, GLfloat value) { ++UniformCalls; Values[location] = {value, 0, 0, 0}; }};
        GlFunctionOverride<PFNGLUNIFORM4FPROC> Uniform4f{glad_glUniform4f, +[](GLint location, GLfloat r, GLfloat g, GLfloat b, GLfloat a) { ++UniformCalls; Values[location] = {r, g, b, a}; }};
        GlFunctionOverride<PFNGLUNIFORM3FPROC> Uniform3f{glad_glUniform3f, +[](GLint location, GLfloat x, GLfloat y, GLfloat z)
        {
            ++UniformCalls;
            Values[location] = {x, y, z, 0};
        }};
        GlFunctionOverride<PFNGLUNIFORMMATRIX4FVPROC> UniformMatrix4fv{glad_glUniformMatrix4fv, +[](GLint, GLsizei, GLboolean, const GLfloat*) { ++UniformCalls; }};
        GlFunctionOverride<PFNGLENABLEPROC> Enable{glad_glEnable, +[](GLenum) {}};
        GlFunctionOverride<PFNGLDISABLEPROC> Disable{glad_glDisable, +[](GLenum) {}};
        GlFunctionOverride<PFNGLACTIVETEXTUREPROC> ActiveTexture{glad_glActiveTexture, +[](GLenum) {}};
        GlFunctionOverride<PFNGLBINDTEXTUREPROC> BindTexture{glad_glBindTexture, +[](GLenum, GLuint) {}};
    };

    rei::assets::AssetRef<rei::render::Shader> MakeBindingShader()
    {
        rei::assets::AssetRef<rei::render::Shader> result("binding-test");
        result.Record = std::make_shared<rei::assets::AssetRecord>();
        result.Record->Id = result.Id;
        result.Record->State = rei::assets::AssetState::Loaded;
        auto shader = std::make_shared<rei::render::Shader>();
        shader->PostLoad();
        result.Record->Value = shader;
        return result;
    }
}

TEST_CASE("Shader caches reflection and absent locations across moves but resets on new programs", "[material][material-binding]")
{
    GlBindingSpy spy;
    GlBindingSpy::UniformCount = 2;
    rei::render::Shader shader;
    shader.PostLoad();
    REQUIRE(GlBindingSpy::ReflectionQueries == 2);
    REQUIRE(shader.GetUniformNamesByType(GL_SAMPLER_2D) == std::vector<std::string>{"tex"});
    REQUIRE(shader.GetUniformNamesByType(GL_FLOAT) == std::vector<std::string>{"value"});
    REQUIRE(shader.GetUniformNamesByType(GL_INT).empty());
    const auto firstLocation = shader.GetLocation("value");
    REQUIRE(shader.GetLocation("value") == firstLocation);
    REQUIRE(shader.GetLocation("absent") == -1);
    REQUIRE(shader.GetLocation("absent") == -1);
    shader.SetFloat("absent", 7);
    REQUIRE(GlBindingSpy::LocationQueries == 2);
    REQUIRE(GlBindingSpy::Values.empty());
    rei::render::Shader moved(std::move(shader));
    REQUIRE(moved.GetLocation("value") == firstLocation);
    REQUIRE(shader.GetLocation("value") == -1);
    REQUIRE(shader.GetUniformNamesByType(GL_FLOAT).empty());
    rei::render::Shader target;
    target.PostLoad();
    target.GetLocation("value");
    target = std::move(moved);
    REQUIRE(target.GetLocation("value") == firstLocation);
    REQUIRE(GlBindingSpy::LocationQueries == 3);
    REQUIRE(GlBindingSpy::ReflectionQueries == 4);
    REQUIRE(GlBindingSpy::Deleted == std::vector<u32>{2});
    target.Delete();
    target.Delete();
    REQUIRE(GlBindingSpy::Deleted == std::vector<u32>{2, 1});
    REQUIRE(target.GetUniformNamesByType(GL_FLOAT).empty());
    target.PostLoad();
    REQUIRE(target.GetLocation("value") != firstLocation);
    REQUIRE(GlBindingSpy::LocationQueries == 4);
    REQUIRE(GlBindingSpy::ReflectionQueries == 6);
}

TEST_CASE("Material cached bindings apply final writes, type changes and alternating shared-shader draws", "[material][material-binding]")
{
    GlBindingSpy spy;
    auto shader = MakeBindingShader();
    Material first(shader), second(shader);
    first.SetFloat("value", 1);
    first.SetFloat("value", 2);
    second.SetFloat("value", 9);
    REQUIRE(GlBindingSpy::Values.empty()); // Writes never touch GL.
    first.Use();
    const auto location = shader->GetLocation("value");
    REQUIRE(GlBindingSpy::Values.at(location)[0] == 2);
    second.Use();
    REQUIRE(GlBindingSpy::Values.at(location)[0] == 9);
    first.Use();
    REQUIRE(GlBindingSpy::Values.at(location)[0] == 2);
    REQUIRE(GlBindingSpy::LocationQueries == 1);
    first.SetColor("value", rei::render::Color(0.2f, 0.4f, 0.6f, 0.8f));
    first.Use();
    const auto linearColor = GlBindingSpy::Values.at(location);
    REQUIRE(std::abs(linearColor[0] - 0.03310477f) < 1e-6f);
    REQUIRE(std::abs(linearColor[1] - 0.13286832f) < 1e-6f);
    REQUIRE(std::abs(linearColor[2] - 0.31854678f) < 1e-6f);
    REQUIRE(linearColor[3] == 0.8f);
    REQUIRE(first.REI_GET().at("Properties").at("value") == nlohmann::json({{"r", 0.2f}, {"g", 0.4f}, {"b", 0.6f}, {"a", 0.8f}}));
    first.SetInt("value", 4);
    first.Use();
    REQUIRE(GlBindingSpy::Values.at(location)[0] == 4);
    first.ClearProperty("value");
    GlBindingSpy::Values.clear();
    first.Use();
    REQUIRE(GlBindingSpy::Values.empty());
    first.REI_SET({{"Properties", {{"value", 5}, {"other", 3.5f}}}});
    first.Use();
    REQUIRE(GlBindingSpy::Values.at(location)[0] == 5);
    first.REI_SET({{"Properties", {{"value", "unsupported"}}}});
    GlBindingSpy::Values.clear();
    first.Use();
    REQUIRE(GlBindingSpy::Values.empty()); // Replacement discards old cached properties too.
    first.SetFloat("value", 12);
    auto replacement = MakeBindingShader();
    // Mimic asset reload in place: public AssetRef record now points at a new shader.
    shader.Record->Value = replacement.Record->Value;
    first.Use();
    const auto reloadedLocation = shader->GetLocation("value");
    REQUIRE(reloadedLocation != location);
    REQUIRE(GlBindingSpy::Values.at(reloadedLocation)[0] == 12);
}

TEST_CASE("Material texture writes stay on CPU and keep deterministic sampler slots", "[material][material-binding]")
{
    GlBindingSpy spy;
    auto shader = MakeBindingShader();
    Material material(shader);
    rei::assets::AssetRef<rei::render::Texture> texture("binding-texture");
    texture.Record = std::make_shared<rei::assets::AssetRecord>();
    texture.Record->Id = texture.Id;
    texture.Record->State = rei::assets::AssetState::Loaded;
    texture.Record->Value = std::make_shared<rei::render::Texture>(1, 1, GL_RGBA, std::vector<u8>{255, 255, 255, 255});
    // Auto-assign normally resolves dependencies. Cached bindings must not invoke it from a setter.
    GlFunctionOverride<rei::assets::AssetRef<rei::render::Texture>::AssignHandler> assignHandler(
        rei::assets::AssetRef<rei::render::Texture>::AssignHandlerFunc,
        +[](auto&, const auto&) { FAIL("Texture setter invoked asset loading"); });
    material.SetTexture("zTexture", texture);
    material.SetTexture("aTexture", texture);
    REQUIRE(GlBindingSpy::LocationQueries == 0);
    REQUIRE(GlBindingSpy::Values.empty());
    material.Use();
    const auto first = shader->GetLocation("aTexture");
    const auto second = shader->GetLocation("zTexture");
    REQUIRE(GlBindingSpy::Values.at(first)[0] == 0);
    REQUIRE(GlBindingSpy::Values.at(second)[0] == 1);
    material.ClearProperty("aTexture");
    material.Use();
    REQUIRE(GlBindingSpy::Values.at(second)[0] == 0);
    material.SetFloat("zTexture", 0.75f);
    material.Use();
    REQUIRE(GlBindingSpy::Values.at(second)[0] == 0.75f);
    material.SetTexture("zTexture", texture);
    material.Use();
    REQUIRE(GlBindingSpy::Values.at(second)[0] == 0);
    REQUIRE(material.REI_GET().at("Properties").at("zTexture").at("Id") == texture.Id);
}

TEST_CASE("Shader profiling counts issued GL calls and restores upload phases", "[profiling][render-profiling][material-binding]")
{
    using namespace rei::profiling;
    GlBindingSpy spy;
    auto shader = MakeBindingShader();
    Material material(shader);
    material.SetFloat("value", 4);
    ProfilingService profiler;
    REQUIRE(profiler.Register(markers::ALL));
    REQUIRE(std::string(profiler.RequestCapture(1).Status) == "queued");
    profiler.BeginFrame();
    shader->Use();
    shader->Use(); // Calls count even when program does not change.
    shader->SetInt("absent", 1);
    shader->SetFloat("absent", 1);
    shader->SetVector3("absent", {1, 2, 3});
    shader->SetColor("absent", rei::render::Color::White());
    shader->SetMatrix4f("absent", glm::mat4(1));
    REQUIRE(GlBindingSpy::UseCalls == 2);
    REQUIRE(GlBindingSpy::UniformCalls == 0);
    shader->SetInt("value", 1);
    shader->SetFloat("value", 2);
    shader->SetVector3("position", {1, 2, 3});
    shader->SetColor("color", rei::render::Color::White());
    shader->SetMatrix4f("custom", glm::mat4(1));
    {
        UniformPhaseScope lighting(UniformPhase::Lighting);
        shader->SetFloat("strength", 1);
        shader->SetViewMatrices(glm::mat4(1), glm::mat4(1), glm::mat4(1));
        material.Use();
        shader->SetFloat("strength", 2); // Nested camera/object/material phases must restore Lighting.
    }
    material.Use(); // Unchanged material still performs its actual GL writes.
    profiler.EndFrame();
    profiler.BeginFrame();
    const auto snapshot = profiler.CopySnapshot(SnapshotView::LastCapture);
    auto value = [&](const Descriptor& descriptor)
    {
        for (u32 index = 0; index < snapshot.MetricCount; ++index)
            if (snapshot.Metrics[index].Id == descriptor.Id) return snapshot.Metrics[index].Value;
        throw std::runtime_error("Expected metric is missing.");
    };
    CHECK(snapshot.InvalidFrames == 0);
    CHECK(snapshot.SampleFrames == 1);
    CHECK(GlBindingSpy::UniformCalls == 12);
    CHECK(value(markers::UNIFORMS) == GlBindingSpy::UniformCalls);
    CHECK(value(markers::UNIFORMS_LIGHTING) == 2);
    CHECK(value(markers::UNIFORMS_CAMERA) == 2);
    CHECK(value(markers::UNIFORMS_OBJECT) == 1);
    CHECK(value(markers::UNIFORMS_MATERIAL) == 2);
    CHECK(value(markers::UNIFORMS_OTHER) == 5);
    CHECK(value(markers::UNIFORMS_LIGHTING) + value(markers::UNIFORMS_CAMERA) + value(markers::UNIFORMS_OBJECT) +
        value(markers::UNIFORMS_MATERIAL) + value(markers::UNIFORMS_OTHER) == value(markers::UNIFORMS));
    CHECK(GlBindingSpy::UseCalls == 16);
    CHECK(value(markers::SHADER_USE_CALLS) == GlBindingSpy::UseCalls);
    CHECK(GlBindingSpy::ProgramSwitches == 1);
    profiler.Shutdown();
}
