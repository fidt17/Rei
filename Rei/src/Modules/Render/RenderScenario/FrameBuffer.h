#pragma once

namespace rei::render
{
    class FrameBuffer
    {
    public:
        FrameBuffer(int width = 0, int height = 0);

        ~FrameBuffer();

        void SetOutputSize(int width, int height);
        
        void EnableBuffer() const;
        void DisableBuffer() const;

        u32 GetColorTexture() const;

    private:
        u32 _fbo; // frame buffer object
        u32 _colorTexture;

        i32 _outputWidth = 1;
        i32 _outputHeight = 1;

        void CreateTexture();
        void DisposeTexture();
    };
}
