#include "pch.h"
#include "catch_amalgamated.hpp"
#include "support/NativeGlFixture.h"
#include "support/RealGlCallTrace.h"
#include "support/AssetTestSupport.h"
#include "Modules/Render/Shaders/ShaderUtility.h"
#include "Modules/Render/Shaders/ShaderGenerator.h"
#include "Modules/Render/Material/Material.h"
#include "Modules/Render/Mesh/Mesh.h"
#include "Modules/Render/RenderScenario/FrameBuffer.h"
#include "Modules/Window/WindowManager.h"
#include "Modules/Input/Input.h"
#include <array>

using namespace rei;
using namespace rei::tests;

namespace
{
    constexpr const char* VERTEX_SOURCE = "#version 330 core\nlayout(location=0) in vec3 position; void main(){gl_Position=vec4(position,1);}";
    constexpr const char* FRAGMENT_SOURCE = "#version 330 core\nout vec4 result; void main(){result=vec4(1,0,0,1);}";
    const std::string MATERIAL_SOURCE = R"(
#ifdef VERTEX
layout(location=0) in vec3 position;
layout(location=2) in vec2 texCoord;
out vec2 uv;
void main(){uv=texCoord;gl_Position=vec4(position,1);}
#endif
#ifdef FRAGMENT
in vec2 uv;
uniform vec4 tint;
out vec4 result;
void main(){result=tint;}
#endif
)";
    const std::string TEXTURE_SOURCE = R"(
#ifdef VERTEX
layout(location=0) in vec3 position;
layout(location=2) in vec2 texCoord;
out vec2 uv;
void main(){uv=texCoord;gl_Position=vec4(position,1);}
#endif
#ifdef FRAGMENT
in vec2 uv;
uniform sampler2D image;
out vec4 result;
void main(){result=texture(image,uv);}
#endif
)";

    std::vector<u8> StringBytes(const std::string& text)
    {
        std::vector<u8> bytes;
        AppendString(bytes, text);
        return bytes;
    }

    std::vector<u8> TextureBytes(const i32 width, const i32 height, const i32 format, const std::vector<u8>& pixels)
    {
        std::vector<u8> bytes;
        for (const auto value : {width, height, format}) AppendInteger(bytes, static_cast<u32>(value), 4);
        AppendInteger(bytes, pixels.size(), 4);
        bytes.insert(bytes.end(), pixels.begin(), pixels.end());
        return bytes;
    }

    void PrepareGlAssets(TemporaryDirectory& files)
    {
        WriteAssetPack(files, {
            {REI_SHADER_INCLUDE_AMBIENT_LIGHT_ASSET_ID, "ambient", StringBytes("")},
            {REI_SHADER_INCLUDE_LIGHTING_ASSET_ID, "point", StringBytes("")},
            {REI_SHADER_INCLUDE_SHADER_COMMON_ASSET_ID, "common", StringBytes("")},
            {REI_SHADER_INCLUDE_VERTEX_COMMON_ASSET_ID, "vertex", StringBytes("")},
            {REI_SHADER_INCLUDE_FRAGMENT_COMMON_ASSET_ID, "fragment", StringBytes("")},
            {"gl-color", "color", StringBytes(MATERIAL_SOURCE)},
            {"gl-texture", "texture", StringBytes(TEXTURE_SOURCE)},
            {REI_WHITE_FALLBACK_TEXTURE_ID, "fallback", TextureBytes(1, 1, GL_RGBA, {255, 255, 255, 255})}
        });
    }

    class Program
    {
    public:
        u32 Id;
        Program(const char* vertex = VERTEX_SOURCE, const char* fragment = FRAGMENT_SOURCE) : Id(render::ShaderUtility().CreateShaderProgram(vertex, fragment)) {}
        ~Program() { if (Id) glDeleteProgram(Id); }
    };

    class Triangle
    {
    public:
        render::Mesh Mesh;
        explicit Triangle(const f32 z = 0) : Mesh("triangle", {
            {{-1, -1, z}, {}, {0, 0}}, {{3, -1, z}, {}, {2, 0}}, {{-1, 3, z}, {}, {0, 2}}
        }, {0, 1, 2}, {}) { Mesh.PostLoad(); }
        ~Triangle() { Mesh.Dispose(); }
    };

    void ClearTarget(const f32 red = 0, const f32 green = 0, const f32 blue = 0, const f32 alpha = 1)
    {
        glViewport(0, 0, 32, 32);
        glClearColor(red, green, blue, alpha);
        glClearDepth(1);
        glClear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT | GL_STENCIL_BUFFER_BIT);
    }

    std::array<i32, 2> TextureSize(const u32 id)
    {
        glBindTexture(GL_TEXTURE_2D, id);
        std::array<i32, 2> size{};
        glGetTexLevelParameteriv(GL_TEXTURE_2D, 0, GL_TEXTURE_WIDTH, &size[0]);
        glGetTexLevelParameteriv(GL_TEXTURE_2D, 0, GL_TEXTURE_HEIGHT, &size[1]);
        return size;
    }
}

TEST_CASE("GL01 valid shader program links and draws real framebuffer pixel", "[native][coverage][coverage-remaining][gl][shader][isolated]")
{
    IsolatedGl([]
    {
        NativeGlFixture gl;
        render::FrameBuffer target(32, 32);
        Program program;
        REQUIRE(program.Id != 0);
        i32 linked = 0;
        glGetProgramiv(program.Id, GL_LINK_STATUS, &linked);
        REQUIRE(linked == GL_TRUE);
        glUseProgram(program.Id);
        Triangle triangle;
        ClearTarget();
        triangle.Mesh.Render();
        RequirePixel(ReadPixel(), {255, 0, 0, 255});
        REQUIRE(glGetError() == GL_NO_ERROR);
    });
}

TEST_CASE("GL02 shader compile failure returns zero without GL error", "[native][coverage][coverage-remaining][gl][shader][isolated]")
{
    IsolatedGl([]
    {
        NativeGlFixture gl;
        SECTION("vertex") { REQUIRE(render::ShaderUtility().CompileVertexShader("#version 330 core\nthis is invalid") == 0); }
        SECTION("fragment") { REQUIRE(render::ShaderUtility().CompileFragmentShader("#version 330 core\nthis is invalid") == 0); }
        REQUIRE(glGetError() == GL_NO_ERROR);
    });
}

TEST_CASE("GL03 second shader failure leaves previous program usable", "[native][coverage][coverage-remaining][gl][shader][isolated]")
{
    IsolatedGl([]
    {
        NativeGlFixture gl;
        Program previous;
        REQUIRE(previous.Id != 0);
        glUseProgram(previous.Id);
        REQUIRE(render::ShaderUtility().CreateShaderProgram(VERTEX_SOURCE, "#version 330 core\ninvalid") == 0);
        i32 current = 0;
        glGetIntegerv(GL_CURRENT_PROGRAM, &current);
        REQUIRE(current == static_cast<i32>(previous.Id));
        REQUIRE(glIsProgram(previous.Id) == GL_TRUE);
        REQUIRE(glGetError() == GL_NO_ERROR);
    });
}

TEST_CASE("GL04 missing vertex entry point rejects link and preserves later creation", "[native][coverage][coverage-remaining][gl][shader][isolated]")
{
    IsolatedGl([]
    {
        NativeGlFixture gl;
        constexpr const char* incompleteVertex = "#version 330 core\nvoid helper(){gl_Position=vec4(0);}";
        const auto compiled = render::ShaderUtility().CompileVertexShader(incompleteVertex);
        REQUIRE(compiled != 0); // Separate compilation succeeds; linking requires a main entry point.
        glDeleteShader(compiled);
        const auto failed = render::ShaderUtility().CreateShaderProgram(incompleteVertex, FRAGMENT_SOURCE);
        REQUIRE(failed == 0);
        Program valid;
        REQUIRE(valid.Id != 0);
        REQUIRE(glGetError() == GL_NO_ERROR);
    });
}

TEST_CASE("GL05 shader move delete and recreate reset native program lifetime", "[native][coverage][coverage-remaining][gl][shader][isolated]")
{
    IsolatedGl([]
    {
        NativeGlFixture gl;
        BehaviourFixture fixture(PrepareGlAssets);
        render::ShaderGenerator::GetInstance().Initialize();
        auto source = fixture.Assets->GetById<render::Shader>("gl-color");
        auto instance = render::Shader::CreateInstanceFrom(*source.Get());
        instance.Use();
        i32 id = 0;
        glGetIntegerv(GL_CURRENT_PROGRAM, &id);
        render::Shader moved(std::move(instance));
        REQUIRE(instance.GetLocation("tint") == -1);
        REQUIRE(moved.GetLocation("tint") >= 0);
        glUseProgram(0);
        moved.Delete();
        moved.Delete();
        REQUIRE(glIsProgram(id) == GL_FALSE);
        moved.PostLoad();
        REQUIRE(moved.GetLocation("tint") >= 0);
        moved.Use();
        REQUIRE(glGetError() == GL_NO_ERROR);
    });
}

TEST_CASE("GL06 material A B A switching restores rendered colors and native uniforms", "[native][coverage][coverage-remaining][gl][material][isolated]")
{
    IsolatedGl([]
    {
        NativeGlFixture gl;
        BehaviourFixture fixture(PrepareGlAssets);
        render::ShaderGenerator::GetInstance().Initialize();
        auto shader = fixture.Assets->GetById<render::Shader>("gl-color");
        render::Material first(shader), second(shader);
        first.SetColor("tint", render::Color(1, 0, 0, 1));
        second.SetColor("tint", render::Color(0, 1, 0, 1));
        first.SetDepth(false);
        second.SetDepth(false);
        render::FrameBuffer target(32, 32);
        Triangle triangle;
        for (auto* material : {&first, &second, &first})
        {
            ClearTarget();
            material->Use();
            triangle.Mesh.Render();
            RequirePixel(ReadPixel(), material == &first ? std::array<u8, 4>{255, 0, 0, 255} : std::array<u8, 4>{0, 255, 0, 255});
            i32 program = 0;
            glGetIntegerv(GL_CURRENT_PROGRAM, &program);
            std::array<f32, 4> uniform{};
            glGetUniformfv(program, shader->GetLocation("tint"), uniform.data());
            REQUIRE(uniform[material == &first ? 0 : 1] == 1.0f);
        }
        REQUIRE(glGetError() == GL_NO_ERROR);
    });
}

TEST_CASE("GL07 staged material write changes pixel only after next Use", "[native][coverage][coverage-remaining][gl][material][isolated]")
{
    IsolatedGl([]
    {
        NativeGlFixture gl;
        BehaviourFixture fixture(PrepareGlAssets);
        render::ShaderGenerator::GetInstance().Initialize();
        render::Material material(fixture.Assets->GetById<render::Shader>("gl-color"));
        material.SetDepth(false);
        material.SetColor("tint", render::Color(1, 0, 0, 1));
        render::FrameBuffer target(32, 32);
        Triangle triangle;
        material.Use();
        material.SetColor("tint", render::Color(0, 0, 1, 1));
        ClearTarget();
        triangle.Mesh.Render();
        RequirePixel(ReadPixel(), {255, 0, 0, 255});
        material.Use();
        ClearTarget();
        triangle.Mesh.Render();
        RequirePixel(ReadPixel(), {0, 0, 255, 255});
    });
}

TEST_CASE("GL08 unloaded material sampler renders actual white fallback", "[native][coverage][coverage-remaining][gl][material][isolated]")
{
    IsolatedGl([]
    {
        NativeGlFixture gl;
        BehaviourFixture fixture(PrepareGlAssets);
        render::ShaderGenerator::GetInstance().Initialize();
        render::Material material(fixture.Assets->GetById<render::Shader>("gl-texture"));
        material.SetDepth(false);
        material.SetTexture("image", assets::AssetRef<render::Texture>("missing-gl-texture"));
        render::FrameBuffer target(32, 32);
        Triangle triangle;
        ClearTarget();
        material.Use();
        triangle.Mesh.Render();
        RequirePixel(ReadPixel(), {255, 255, 255, 255});
        REQUIRE(material.REI_GET().at("Properties").at("image").at("Id") == "missing-gl-texture");
        REQUIRE(glGetError() == GL_NO_ERROR);
    });
}

TEST_CASE("GL09 depth enabled occludes far mesh and disabling permits overwrite", "[native][coverage][coverage-remaining][gl][material][depth][isolated]")
{
    IsolatedGl([]
    {
        NativeGlFixture gl;
        BehaviourFixture fixture(PrepareGlAssets);
        render::ShaderGenerator::GetInstance().Initialize();
        auto shader = fixture.Assets->GetById<render::Shader>("gl-color");
        render::Material nearMaterial(shader), farMaterial(shader);
        nearMaterial.SetColor("tint", render::Color(1, 0, 0, 1));
        farMaterial.SetColor("tint", render::Color(0, 1, 0, 1));
        render::FrameBuffer target(32, 32);
        Triangle nearTriangle(-0.5f), farTriangle(0.5f);
        glDepthFunc(GL_LESS);
        ClearTarget();
        nearMaterial.Use();
        nearTriangle.Mesh.Render();
        farMaterial.Use();
        farTriangle.Mesh.Render();
        RequirePixel(ReadPixel(), {255, 0, 0, 255});
        farMaterial.SetDepth(false);
        farMaterial.Use();
        REQUIRE(glIsEnabled(GL_DEPTH_TEST) == GL_FALSE);
        farTriangle.Mesh.Render();
        RequirePixel(ReadPixel(), {0, 255, 0, 255});
    });
}

TEST_CASE("GL10 RGBA texture upload preserves row orientation and alpha", "[native][coverage][coverage-remaining][gl][texture][isolated]")
{
    IsolatedGl([]
    {
        NativeGlFixture gl;
        const std::vector<u8> input = {255, 0, 0, 128, 0, 255, 0, 64, 0, 0, 255, 255, 255, 255, 0, 0};
        render::Texture texture(2, 2, GL_RGBA, input);
        texture.PostLoad();
        std::vector<u8> output(input.size());
        texture.Use();
        glGetTexImage(GL_TEXTURE_2D, 0, GL_RGBA, GL_UNSIGNED_BYTE, output.data());
        REQUIRE(output == input);
        REQUIRE(TextureSize(texture.GetId()) == std::array<i32, 2>{2, 2});
        const auto id = texture.GetId();
        glDeleteTextures(1, &id); // Texture has no native Dispose/destructor API.
    });
}

TEST_CASE("GL11 odd RGB row upload preserves tightly packed source bytes", "[native][coverage][coverage-remaining][gl][texture][isolated]")
{
    IsolatedGl([]
    {
        NativeGlFixture gl;
        const std::vector<u8> expected = {255, 0, 0, 0, 255, 0, 0, 0, 255, 255, 255, 0, 255, 0, 255, 0, 255, 255};
        auto input = expected;
        input.insert(input.end(), {19, 23, 29}); // Owned canary padding: buggy unpack must not read outside allocation.
        REQUIRE([] { i32 alignment = 0; glGetIntegerv(GL_UNPACK_ALIGNMENT, &alignment); return alignment; }() == 4);
        render::Texture texture(3, 2, GL_RGB, input);
        texture.PostLoad();
        glPixelStorei(GL_PACK_ALIGNMENT, 1);
        std::vector<u8> output(expected.size());
        texture.Use();
        glGetTexImage(GL_TEXTURE_2D, 0, GL_RGB, GL_UNSIGNED_BYTE, output.data());
        const auto id = texture.GetId();
        glDeleteTextures(1, &id);
        REQUIRE(output == expected);
    });
}

TEST_CASE("GL12 texture PostLoad is idempotent and binds requested sampler unit", "[native][coverage][coverage-remaining][gl][texture][isolated]")
{
    IsolatedGl([]
    {
        NativeGlFixture gl;
        render::Texture texture(1, 1, GL_RGBA, {3, 5, 7, 255});
        texture.PostLoad();
        const auto id = texture.GetId();
        texture.PostLoad();
        REQUIRE(texture.GetId() == id);
        texture.Use(3);
        i32 unit = 0, bound = 0;
        glGetIntegerv(GL_ACTIVE_TEXTURE, &unit);
        glGetIntegerv(GL_TEXTURE_BINDING_2D, &bound);
        REQUIRE(unit == GL_TEXTURE3);
        REQUIRE(bound == static_cast<i32>(id));
        glDeleteTextures(1, &id);
        REQUIRE(glGetError() == GL_NO_ERROR);
    });
}

TEST_CASE("GL13 releasing texture asset deletes real driver texture", "[native][coverage][coverage-remaining][gl][texture][isolated]")
{
    IsolatedGl([]
    {
        NativeGlFixture gl;
        BehaviourFixture fixture(PrepareGlAssets);
        auto texture = fixture.Assets->GetById<render::Texture>(REI_WHITE_FALLBACK_TEXTURE_ID);
        REQUIRE(texture.IsLoaded());
        const auto id = texture->GetId();
        REQUIRE(glIsTexture(id) == GL_TRUE);
        fixture.Assets->Release(texture);
        const bool deleted = glIsTexture(id) == GL_FALSE;
        if (!deleted) glDeleteTextures(1, &id);
        REQUIRE(deleted);
    });
}

TEST_CASE("GL14 mesh upload exposes exact index and vertex buffer contents", "[native][coverage][coverage-remaining][gl][mesh][isolated]")
{
    IsolatedGl([]
    {
        NativeGlFixture gl;
        Triangle triangle;
        auto& mesh = triangle.Mesh;
        REQUIRE(glIsVertexArray(mesh.VAO) == GL_TRUE);
        glBindBuffer(GL_ARRAY_BUFFER, mesh.VBO);
        i32 bytes = 0;
        glGetBufferParameteriv(GL_ARRAY_BUFFER, GL_BUFFER_SIZE, &bytes);
        REQUIRE(bytes == sizeof(render::Vertex) * 3);
        std::array<render::Vertex, 3> vertices{};
        glGetBufferSubData(GL_ARRAY_BUFFER, 0, sizeof(vertices), vertices.data());
        REQUIRE(vertices[1].Position.x == 3.0f);
        REQUIRE(vertices[2].TexCoords.y == 2.0f);
        glBindVertexArray(mesh.VAO);
        std::array<u32, 3> indices{};
        glGetBufferSubData(GL_ELEMENT_ARRAY_BUFFER, 0, sizeof(indices), indices.data());
        REQUIRE(indices == std::array<u32, 3>{0, 1, 2});
        glBindVertexArray(0);
        REQUIRE(glGetError() == GL_NO_ERROR);
    });
}

TEST_CASE("GL15 empty mesh draws no pixel and disposes allocated buffers", "[native][coverage][coverage-remaining][gl][mesh][isolated]")
{
    IsolatedGl([]
    {
        NativeGlFixture gl;
        render::FrameBuffer target(32, 32);
        Program program;
        glUseProgram(program.Id);
        render::Mesh mesh("empty", {}, {}, {});
        mesh.PostLoad();
        ClearTarget(0, 0, 1);
        mesh.Render();
        RequirePixel(ReadPixel(), {0, 0, 255, 255});
        const auto vao = mesh.VAO, vbo = mesh.VBO, ebo = mesh.EBO;
        mesh.Dispose();
        mesh.Dispose();
        REQUIRE(glIsVertexArray(vao) == GL_FALSE);
        REQUIRE(glIsBuffer(vbo) == GL_FALSE);
        REQUIRE(glIsBuffer(ebo) == GL_FALSE);
        REQUIRE(glGetError() == GL_NO_ERROR);
    });
}

TEST_CASE("GL16 disposed mesh can PostLoad again and render its retained CPU geometry", "[native][coverage][coverage-remaining][gl][mesh][isolated][proposed-policy]")
{
    IsolatedGl([]
    {
        NativeGlFixture gl;
        render::FrameBuffer target(32, 32);
        Program program;
        glUseProgram(program.Id);
        Triangle triangle;
        triangle.Mesh.Dispose();
        triangle.Mesh.PostLoad();
        REQUIRE(glIsVertexArray(triangle.Mesh.VAO) == GL_TRUE);
        ClearTarget();
        triangle.Mesh.Render();
        RequirePixel(ReadPixel(), {255, 0, 0, 255});
        REQUIRE(glGetError() == GL_NO_ERROR);
    });
}

TEST_CASE("GL17 framebuffer starts complete and reads expected clear RGBA", "[native][coverage][coverage-remaining][gl][framebuffer][isolated]")
{
    IsolatedGl([]
    {
        NativeGlFixture gl;
        render::FrameBuffer target(32, 32);
        REQUIRE(glCheckFramebufferStatus(GL_FRAMEBUFFER) == GL_FRAMEBUFFER_COMPLETE);
        REQUIRE(TextureSize(target.GetColorTexture()) == std::array<i32, 2>{32, 32});
        ClearTarget(0.25f, 0.5f, 0.75f, 1);
        RequirePixel(ReadPixel(), {64, 128, 191, 255});
    });
}

TEST_CASE("GL18 unchanged framebuffer size preserves color and depth allocations", "[native][coverage][coverage-remaining][gl][framebuffer][isolated]")
{
    IsolatedGl([]
    {
        NativeGlFixture gl;
        render::FrameBuffer target(32, 32);
        const auto color = target.GetColorTexture();
        i32 depth = 0;
        glGetFramebufferAttachmentParameteriv(GL_FRAMEBUFFER, GL_DEPTH_STENCIL_ATTACHMENT, GL_FRAMEBUFFER_ATTACHMENT_OBJECT_NAME, &depth);
        RealGlCallTrace trace;
        for (u32 i = 0; i < 5; ++i) target.EnableBuffer(32, 32);
        i32 after = 0;
        glGetFramebufferAttachmentParameteriv(GL_FRAMEBUFFER, GL_DEPTH_STENCIL_ATTACHMENT, GL_FRAMEBUFFER_ATTACHMENT_OBJECT_NAME, &after);
        REQUIRE(target.GetColorTexture() == color);
        REQUIRE(after == depth);
        CHECK(trace.TextureStorage.empty());
        CHECK(trace.RenderbufferStorage.empty());
        REQUIRE(glCheckFramebufferStatus(GL_FRAMEBUFFER) == GL_FRAMEBUFFER_COMPLETE);
    });
}

TEST_CASE("GL19 framebuffer resize replaces storage and keeps attachments complete", "[native][coverage][coverage-remaining][gl][framebuffer][isolated]")
{
    IsolatedGl([]
    {
        NativeGlFixture gl;
        render::FrameBuffer target(32, 32);
        RealGlCallTrace trace;
        target.EnableBuffer(7, 5);
        REQUIRE(trace.TextureStorage.size() == 1);
        REQUIRE(trace.RenderbufferStorage.size() == 1);
        CHECK(trace.TextureStorage.front().Width == 7);
        CHECK(trace.TextureStorage.front().Height == 5);
        CHECK(trace.RenderbufferStorage.front().Width == 7);
        CHECK(trace.RenderbufferStorage.front().Height == 5);
        REQUIRE(TextureSize(target.GetColorTexture()) == std::array<i32, 2>{7, 5});
        REQUIRE(glCheckFramebufferStatus(GL_FRAMEBUFFER) == GL_FRAMEBUFFER_COMPLETE);
        glViewport(0, 0, 7, 5);
        glClearColor(0, 1, 0, 1);
        glClear(GL_COLOR_BUFFER_BIT);
        RequirePixel(ReadPixel(6, 4), {0, 255, 0, 255});
        REQUIRE(glGetError() == GL_NO_ERROR);
    });
}

TEST_CASE("GL20 zero-size framebuffer restores complete storage on nonzero resize", "[native][coverage][coverage-remaining][gl][framebuffer][isolated]")
{
    IsolatedGl([]
    {
        NativeGlFixture gl;
        render::FrameBuffer target;
        REQUIRE(target.GetColorTexture() == 0);
        target.EnableBuffer(0, 0);
        target.EnableBuffer(32, 32);
        REQUIRE(glCheckFramebufferStatus(GL_FRAMEBUFFER) == GL_FRAMEBUFFER_COMPLETE);
        REQUIRE(TextureSize(target.GetColorTexture()) == std::array<i32, 2>{32, 32});
        ClearTarget(1, 0, 1);
        RequirePixel(ReadPixel(), {255, 0, 255, 255});
        target.EnableBuffer(0, 0);
        target.EnableBuffer(4, 3);
        REQUIRE(TextureSize(target.GetColorTexture()) == std::array<i32, 2>{4, 3});
        REQUIRE(glCheckFramebufferStatus(GL_FRAMEBUFFER) == GL_FRAMEBUFFER_COMPLETE);
    });
}

TEST_CASE("GL21 framebuffer destructor releases color depth and framebuffer objects", "[native][coverage][coverage-remaining][gl][framebuffer][isolated]")
{
    IsolatedGl([]
    {
        NativeGlFixture gl;
        u32 color = 0;
        i32 framebuffer = 0, depth = 0;
        {
            render::FrameBuffer target(32, 32);
            color = target.GetColorTexture();
            glGetIntegerv(GL_FRAMEBUFFER_BINDING, &framebuffer);
            glGetFramebufferAttachmentParameteriv(GL_FRAMEBUFFER, GL_DEPTH_STENCIL_ATTACHMENT, GL_FRAMEBUFFER_ATTACHMENT_OBJECT_NAME, &depth);
        }
        REQUIRE(glIsTexture(color) == GL_FALSE);
        REQUIRE(glIsRenderbuffer(depth) == GL_FALSE);
        REQUIRE(glIsFramebuffer(framebuffer) == GL_FALSE);
        REQUIRE(glGetError() == GL_NO_ERROR);
    });
}

TEST_CASE("GL22 Window Close emits ordered callbacks exactly once", "[native][coverage][coverage-remaining][gl][window][isolated]")
{
    IsolatedGl([]
    {
        NativeGlFixture gl;
        window::WindowManager manager;
        auto window = manager.NewWindow({"native close", 32, 32, true, false, false});
        std::vector<std::string> calls;
        window->WindowClosingEvent.append([&](auto&) { calls.push_back("closing"); });
        window->WindowClosedEvent.append([&](auto&) { calls.push_back("closed"); });
        window->Close();
        window->Close();
        manager.CloseAll();
        REQUIRE(calls == std::vector<std::string>{"closing", "closed"});
    });
}

TEST_CASE("GL23 closing first WindowManager window keeps later window owned", "[native][coverage][coverage-remaining][gl][window][isolated]")
{
    IsolatedGl([]
    {
        NativeGlFixture gl;
        window::WindowManager manager;
        auto first = manager.NewWindow({"first", 32, 32, true, false, false});
        auto second = manager.NewWindow({"second", 32, 32, true, false, false});
        i32 firstCloses = 0, secondCloses = 0;
        first->WindowClosedEvent.append([&](auto&) { ++firstCloses; });
        second->WindowClosedEvent.append([&](auto&) { ++secondCloses; });
        manager.CloseWindow(*first);
        CHECK(firstCloses == 1);
        CHECK(secondCloses == 0);
        manager.CloseAll();
        const auto closedByManager = secondCloses;
        second->Close(); // Fixture cleanup even if ownership contract failed.
        REQUIRE(closedByManager == 1);
    });
}

TEST_CASE("GL24 closing middle WindowManager window keeps both neighbors owned", "[native][coverage][coverage-remaining][gl][window][isolated]")
{
    IsolatedGl([]
    {
        NativeGlFixture gl;
        window::WindowManager manager;
        auto first = manager.NewWindow({"first", 32, 32, true, false, false});
        auto middle = manager.NewWindow({"middle", 32, 32, true, false, false});
        auto last = manager.NewWindow({"last", 32, 32, true, false, false});
        std::array<i32, 3> closed{};
        first->WindowClosedEvent.append([&](auto&) { ++closed[0]; });
        middle->WindowClosedEvent.append([&](auto&) { ++closed[1]; });
        last->WindowClosedEvent.append([&](auto&) { ++closed[2]; });
        manager.CloseWindow(*middle);
        manager.CloseAll();
        const auto managerClosed = closed;
        first->Close(); middle->Close(); last->Close();
        REQUIRE(managerClosed == std::array<i32, 3>{1, 1, 1});
    });
}

TEST_CASE("GL25 window close request may remove itself during manager update", "[native][coverage][coverage-remaining][gl][window][isolated]")
{
    IsolatedGl([]
    {
        NativeGlFixture gl;
        window::WindowManager manager;
        auto first = manager.NewWindow({"first", 32, 32, true, false, false});
        auto second = manager.NewWindow({"second", 32, 32, true, false, false});
        i32 closes = 0;
        first->CloseRequestEvent.append([&] { manager.CloseWindow(*first); });
        second->WindowClosedEvent.append([&](auto&) { ++closes; });
        glfwSetWindowShouldClose(first->GetGLFWWindow(), GLFW_TRUE);
        manager.OnUpdate();
        manager.CloseAll();
        const auto managerCloses = closes;
        second->Close();
        REQUIRE(managerCloses == 1);
    });
}

TEST_CASE("GL26 odd single-channel upload preserves tight rows", "[native][coverage][coverage-remaining][gl][texture][isolated]")
{
    IsolatedGl([]
    {
        NativeGlFixture gl;
        const std::vector<u8> expected = {11, 22, 33, 44, 55, 66};
        auto padded = expected;
        padded.push_back(77);
        render::Texture texture(3, 2, GL_RED, padded);
        texture.PostLoad();
        texture.Use();
        glPixelStorei(GL_PACK_ALIGNMENT, 1);
        std::vector<u8> actual(expected.size());
        glGetTexImage(GL_TEXTURE_2D, 0, GL_RED, GL_UNSIGNED_BYTE, actual.data());
        const auto id = texture.GetId();
        glDeleteTextures(1, &id);
        REQUIRE(actual == expected);
    });
}

TEST_CASE("GL27 odd two-channel upload preserves tight rows", "[native][coverage][coverage-remaining][gl][texture][isolated]")
{
    IsolatedGl([]
    {
        NativeGlFixture gl;
        const std::vector<u8> expected = {11, 12, 21, 22, 31, 32, 41, 42, 51, 52, 61, 62};
        auto padded = expected;
        padded.insert(padded.end(), {71, 72});
        render::Texture texture(3, 2, GL_RG, padded);
        texture.PostLoad();
        texture.Use();
        glPixelStorei(GL_PACK_ALIGNMENT, 1);
        std::vector<u8> actual(expected.size());
        glGetTexImage(GL_TEXTURE_2D, 0, GL_RG, GL_UNSIGNED_BYTE, actual.data());
        const auto id = texture.GetId();
        glDeleteTextures(1, &id);
        REQUIRE(actual == expected);
    });
}

TEST_CASE("GL28 mesh repeat PostLoad keeps same native allocations", "[native][coverage][coverage-remaining][gl][mesh][isolated]")
{
    IsolatedGl([]
    {
        NativeGlFixture gl;
        Triangle triangle;
        const auto vao = triangle.Mesh.VAO, vbo = triangle.Mesh.VBO, ebo = triangle.Mesh.EBO;
        for (u32 i = 0; i < 4; ++i) triangle.Mesh.PostLoad();
        REQUIRE(triangle.Mesh.VAO == vao);
        REQUIRE(triangle.Mesh.VBO == vbo);
        REQUIRE(triangle.Mesh.EBO == ebo);
        REQUIRE(glIsBuffer(vbo) == GL_TRUE);
        REQUIRE(glGetError() == GL_NO_ERROR);
    });
}

TEST_CASE("GL29 Texture creates complete mip chain with documented repeat and filter parameters", "[native][coverage][coverage-remaining][gl][texture][isolated]")
{
    IsolatedGl([]
    {
        NativeGlFixture gl;
        render::Texture texture(4, 2, GL_RGBA, std::vector<u8>(4 * 2 * 4, 255));
        texture.PostLoad();
        texture.Use();
        i32 width = 0, height = 0;
        glGetTexLevelParameteriv(GL_TEXTURE_2D, 2, GL_TEXTURE_WIDTH, &width);
        glGetTexLevelParameteriv(GL_TEXTURE_2D, 2, GL_TEXTURE_HEIGHT, &height);
        REQUIRE(width == 1);
        REQUIRE(height == 1);
        for (const auto& [parameter, expected] : std::array<std::pair<GLenum, i32>, 4>{
            {{GL_TEXTURE_WRAP_S, GL_REPEAT}, {GL_TEXTURE_WRAP_T, GL_REPEAT},
            {GL_TEXTURE_MIN_FILTER, GL_LINEAR_MIPMAP_LINEAR}, {GL_TEXTURE_MAG_FILTER, GL_LINEAR}}})
        {
            i32 value = 0;
            glGetTexParameteriv(GL_TEXTURE_2D, parameter, &value);
            REQUIRE(value == expected);
        }
        const auto id = texture.GetId();
        glDeleteTextures(1, &id);
        REQUIRE(glGetError() == GL_NO_ERROR);
    });
}

TEST_CASE("GL30 mouse snapshot reads source window-local coordinates and dimensions", "[native][coverage][coverage-remaining][gl][input][isolated]")
{
    IsolatedGl([]
    {
        NativeGlFixture gl;
        Input::SetSource(gl.Window());
        const auto cursor = glfwSetCursorPosCallback(gl.Window(), nullptr);
        REQUIRE(cursor != nullptr);
        glfwSetCursorPosCallback(gl.Window(), cursor);
        cursor(gl.Window(), 7.25, 11.5);
        const auto state = Input::GetMouseState();
        REQUIRE(state.X == 7.25f);
        REQUIRE(state.Y == 11.5f);
        REQUIRE(state.Width == 32);
        REQUIRE(state.Height == 32);
        REQUIRE(state.HasFocus == (glfwGetWindowAttrib(gl.Window(), GLFW_FOCUSED) == GLFW_TRUE));
        REQUIRE(state.IsHovered == (glfwGetWindowAttrib(gl.Window(), GLFW_HOVERED) == GLFW_TRUE));
    });
}

TEST_CASE("GL31 mouse snapshot requires source window to own current context", "[native][coverage][coverage-remaining][gl][input][isolated]")
{
    IsolatedGl([]
    {
        NativeGlFixture gl;
        Input::SetSource(gl.Window());
        glfwMakeContextCurrent(nullptr);
        const auto state = Input::GetMouseState();
        glfwMakeContextCurrent(gl.Window());
        REQUIRE(state.X == 0);
        REQUIRE(state.Y == 0);
        REQUIRE(state.Width == 0);
        REQUIRE(state.Height == 0);
        REQUIRE_FALSE(state.HasFocus);
        REQUIRE_FALSE(state.IsHovered);
    });
}

TEST_CASE("GL32 switching mouse source rejects snapshot from different current window", "[native][coverage][coverage-remaining][gl][input][isolated]")
{
    IsolatedGl([]
    {
        NativeGlFixture gl;
        std::unique_ptr<GLFWwindow, decltype(&glfwDestroyWindow)> other(glfwCreateWindow(21, 13, "other hidden input source", nullptr, nullptr), &glfwDestroyWindow);
        REQUIRE(other != nullptr);
        Input::SetSource(other.get());
        const auto absent = Input::GetMouseState();
        CHECK(absent.Width == 0);
        CHECK(absent.Height == 0);
        glfwMakeContextCurrent(other.get());
        const auto present = Input::GetMouseState();
        glfwMakeContextCurrent(gl.Window());
        Input::SetSource(gl.Window());
        REQUIRE(present.Width == 21);
        REQUIRE(present.Height == 13);
    });
}
