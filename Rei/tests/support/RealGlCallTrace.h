#pragma once
#include "NativeGlFixture.h"

namespace rei::tests
{
    // Observes actual driver calls through GLAD. Every wrapper forwards exactly
    // once; it neither substitutes results nor owns/deletes observed GL objects.
    // Construct after context/GLAD setup, use on one isolated child thread, and
    // destroy before context teardown. Reloading GLAD inside this scope is invalid.
    class RealGlCallTrace
    {
    public:
        struct ShaderCreation { u32 Id; u32 Type; };
        struct StorageAllocation { i32 Width; i32 Height; };
        struct ArrayDraw { u32 Mode; i32 First; i32 Count; u32 Texture; };

        std::vector<ShaderCreation> CreatedShaders;
        std::vector<u32> DeletedShaders;
        std::vector<u32> CreatedPrograms;
        std::vector<u32> DeletedPrograms;
        std::vector<StorageAllocation> TextureStorage;
        std::vector<StorageAllocation> RenderbufferStorage;
        std::vector<ArrayDraw> ArrayDraws;

        RealGlCallTrace()
        {
            REQUIRE(IsIsolatedChild());
            REQUIRE(glfwGetCurrentContext() != nullptr);
            REQUIRE(_active == nullptr);
            _active = this;
            glad_glCreateShader = CreateShader;
            glad_glDeleteShader = DeleteShader;
            glad_glCreateProgram = CreateProgram;
            glad_glDeleteProgram = DeleteProgram;
            glad_glTexImage2D = TexImage2D;
            glad_glRenderbufferStorage = AllocateRenderbuffer;
            glad_glDrawArrays = DrawArrays;
        }

        RealGlCallTrace(const RealGlCallTrace&) = delete;
        RealGlCallTrace& operator=(const RealGlCallTrace&) = delete;

        ~RealGlCallTrace()
        {
            glad_glCreateShader = _createShader;
            glad_glDeleteShader = _deleteShader;
            glad_glCreateProgram = _createProgram;
            glad_glDeleteProgram = _deleteProgram;
            glad_glTexImage2D = _texImage2D;
            glad_glRenderbufferStorage = _renderbufferStorage;
            glad_glDrawArrays = _drawArrays;
            _active = nullptr;
        }

    private:
        static inline thread_local RealGlCallTrace* _active = nullptr;
        PFNGLCREATESHADERPROC _createShader = glad_glCreateShader;
        PFNGLDELETESHADERPROC _deleteShader = glad_glDeleteShader;
        PFNGLCREATEPROGRAMPROC _createProgram = glad_glCreateProgram;
        PFNGLDELETEPROGRAMPROC _deleteProgram = glad_glDeleteProgram;
        PFNGLTEXIMAGE2DPROC _texImage2D = glad_glTexImage2D;
        PFNGLRENDERBUFFERSTORAGEPROC _renderbufferStorage = glad_glRenderbufferStorage;
        PFNGLDRAWARRAYSPROC _drawArrays = glad_glDrawArrays;

        static GLuint APIENTRY CreateShader(GLenum type)
        {
            const auto id = _active->_createShader(type);
            _active->CreatedShaders.push_back({id, type});
            return id;
        }

        static void APIENTRY DeleteShader(GLuint id)
        {
            _active->_deleteShader(id);
            _active->DeletedShaders.push_back(id);
        }

        static GLuint APIENTRY CreateProgram()
        {
            const auto id = _active->_createProgram();
            _active->CreatedPrograms.push_back(id);
            return id;
        }

        static void APIENTRY DeleteProgram(GLuint id)
        {
            _active->_deleteProgram(id);
            _active->DeletedPrograms.push_back(id);
        }

        static void APIENTRY TexImage2D(GLenum target, GLint level, GLint internalFormat, GLsizei width, GLsizei height, GLint border, GLenum format, GLenum type, const void* pixels)
        {
            _active->_texImage2D(target, level, internalFormat, width, height, border, format, type, pixels);
            _active->TextureStorage.push_back({width, height});
        }

        static void APIENTRY AllocateRenderbuffer(GLenum target, GLenum internalFormat, GLsizei width, GLsizei height)
        {
            _active->_renderbufferStorage(target, internalFormat, width, height);
            _active->RenderbufferStorage.push_back({width, height});
        }

        static void APIENTRY DrawArrays(GLenum mode, GLint first, GLsizei count)
        {
            i32 texture = 0;
            glGetIntegerv(GL_TEXTURE_BINDING_2D, &texture);
            _active->_drawArrays(mode, first, count);
            _active->ArrayDraws.push_back({mode, first, count, static_cast<u32>(texture)});
        }
    };
}
