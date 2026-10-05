#include "pch.h"
#include "support/RealGlCallTrace.h"
#include "Modules/Render/Shaders/ShaderUtility.h"
#include <algorithm>

using namespace rei;
using namespace rei::tests;

namespace
{
    constexpr const char* VALID_VERTEX = "#version 330 core\nvoid main(){gl_Position=vec4(0,0,0,1);}";
    constexpr const char* VALID_FRAGMENT = "#version 330 core\nout vec4 result; void main(){result=vec4(1);}";
    constexpr const char* INVALID_SHADER = "#version 330 core\nthis is invalid shader source";

    void RequireReleasedObjects(const RealGlCallTrace& trace)
    {
        CHECK(trace.DeletedShaders.size() == trace.CreatedShaders.size());
        CHECK(trace.DeletedPrograms.size() == trace.CreatedPrograms.size());
        for (const auto& shader : trace.CreatedShaders)
        {
            CAPTURE(shader.Id, shader.Type);
            REQUIRE(shader.Id != 0);
            CHECK(std::count(trace.DeletedShaders.begin(), trace.DeletedShaders.end(), shader.Id) == 1);
            CHECK(glIsShader(shader.Id) == GL_FALSE);
        }
        for (const auto program : trace.CreatedPrograms)
        {
            CAPTURE(program);
            REQUIRE(program != 0);
            CHECK(std::count(trace.DeletedPrograms.begin(), trace.DeletedPrograms.end(), program) == 1);
            CHECK(glIsProgram(program) == GL_FALSE);
        }
        CHECK(glGetError() == GL_NO_ERROR);
    }
}

TEST_CASE("SHADER_CLEANUP01 failed vertex releases actual shader and creates no program", "[native][coverage][coverage-remaining][gl][shader][isolated]")
{
    IsolatedGl([]
    {
        NativeGlFixture gl;
        RealGlCallTrace trace;
        REQUIRE(render::ShaderUtility().CreateShaderProgram(INVALID_SHADER, VALID_FRAGMENT) == 0);
        REQUIRE(trace.CreatedShaders.size() == 1);
        CHECK(trace.CreatedShaders.front().Type == GL_VERTEX_SHADER);
        CHECK(trace.CreatedPrograms.empty());
        RequireReleasedObjects(trace);
    });
}

TEST_CASE("SHADER_CLEANUP02 failed fragment releases both actual shaders", "[native][coverage][coverage-remaining][gl][shader][isolated]")
{
    IsolatedGl([]
    {
        NativeGlFixture gl;
        RealGlCallTrace trace;
        REQUIRE(render::ShaderUtility().CreateShaderProgram(VALID_VERTEX, INVALID_SHADER) == 0);
        REQUIRE(trace.CreatedShaders.size() == 2);
        CHECK(trace.CreatedShaders[0].Type == GL_VERTEX_SHADER);
        CHECK(trace.CreatedShaders[1].Type == GL_FRAGMENT_SHADER);
        CHECK(trace.CreatedPrograms.empty());
        RequireReleasedObjects(trace);
    });
}

TEST_CASE("SHADER_CLEANUP03 failed link releases actual program and attached shaders", "[native][coverage][coverage-remaining][gl][shader][isolated]")
{
    IsolatedGl([]
    {
        NativeGlFixture gl;
        RealGlCallTrace trace;
        // This compiles as a translation unit; linking requires vertex main.
        const char* helperOnlyVertex = "#version 330 core\nvec4 helper(){return vec4(0);}";
        REQUIRE(render::ShaderUtility().CreateShaderProgram(helperOnlyVertex, VALID_FRAGMENT) == 0);
        REQUIRE(trace.CreatedShaders.size() == 2);
        REQUIRE(trace.CreatedPrograms.size() == 1);
        RequireReleasedObjects(trace);
    });
}

TEST_CASE("SHADER_CLEANUP04 successful program owns pending shader deletion until program release", "[native][coverage][coverage-remaining][gl][shader][isolated]")
{
    IsolatedGl([]
    {
        NativeGlFixture gl;
        const auto createShader = glad_glCreateShader;
        const auto deleteShader = glad_glDeleteShader;
        const auto createProgram = glad_glCreateProgram;
        const auto deleteProgram = glad_glDeleteProgram;
        const auto texImage = glad_glTexImage2D;
        const auto renderbufferStorage = glad_glRenderbufferStorage;
        const auto drawArrays = glad_glDrawArrays;
        {
            RealGlCallTrace trace;
            const auto program = render::ShaderUtility().CreateShaderProgram(VALID_VERTEX, VALID_FRAGMENT);
            REQUIRE(program != 0);
            REQUIRE(trace.CreatedPrograms.size() == 1);
            REQUIRE(trace.CreatedShaders.size() == 2);
            REQUIRE(trace.DeletedShaders.size() == 2);
            CHECK(trace.DeletedPrograms.empty());
            CHECK(glIsProgram(program) == GL_TRUE);
            for (const auto& shader : trace.CreatedShaders)
            {
                i32 pendingDeletion = 0;
                glGetShaderiv(shader.Id, GL_DELETE_STATUS, &pendingDeletion);
                CHECK(pendingDeletion == GL_TRUE);
                CHECK(glIsShader(shader.Id) == GL_TRUE);
            }
            glDeleteProgram(program);
            RequireReleasedObjects(trace);
        }
        CHECK(glad_glCreateShader == createShader);
        CHECK(glad_glDeleteShader == deleteShader);
        CHECK(glad_glCreateProgram == createProgram);
        CHECK(glad_glDeleteProgram == deleteProgram);
        CHECK(glad_glTexImage2D == texImage);
        CHECK(glad_glRenderbufferStorage == renderbufferStorage);
        CHECK(glad_glDrawArrays == drawArrays);
    });
}
