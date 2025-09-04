#pragma once

namespace rei::render
{
    class FrameBuffer
    {
    public:
        FrameBuffer(int width = 0, int height = 0);

        ~FrameBuffer();

        void EnableBuffer(int width, int height);

        u32 GetColorTexture() const;

    private:
        u32 _fbo = 0; // frame buffer object
        u32 _colorTexture = 0;
        u32 _rbo = 0; // render buffer object for depth and stencil

        i32 _outputWidth = 1;
        i32 _outputHeight = 1;

        void CreateTextures();
        void DisposeTextures() const;
    };
}
