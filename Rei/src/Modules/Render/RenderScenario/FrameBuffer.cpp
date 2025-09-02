#include "FrameBuffer.h"

#include "glad/glad.h"

rei::render::FrameBuffer::FrameBuffer(const int width, const int height)
    : _outputWidth(width), _outputHeight(height)
{
    glGenFramebuffers(1, &_fbo);
    glBindFramebuffer(GL_FRAMEBUFFER, _fbo);

    CreateTexture();
}

rei::render::FrameBuffer::~FrameBuffer()
{
    glDeleteFramebuffers(1, &_fbo);
    DisposeTexture();
}

void rei::render::FrameBuffer::SetOutputSize(int width, int height)
{
    const bool updateTexture = _outputWidth != width || _outputHeight != height;
    _outputWidth = width;
    _outputHeight = height;

    if (updateTexture)
    {
        CreateTexture();
    }
}

void rei::render::FrameBuffer::EnableBuffer() const
{
    if (glCheckFramebufferStatus(GL_FRAMEBUFFER) != GL_FRAMEBUFFER_COMPLETE)
    {
        LOG_ERROR("FrameBuffer setup is not complete")
    }

    glBindFramebuffer(GL_FRAMEBUFFER, _fbo);
}

void rei::render::FrameBuffer::DisableBuffer() const
{
    glBindFramebuffer(GL_FRAMEBUFFER, 0);
}

u32 rei::render::FrameBuffer::GetColorTexture() const
{
    return _colorTexture;
}

void rei::render::FrameBuffer::CreateTexture()
{
    if (_outputWidth == 0 || _outputHeight == 0) return;

    if (_colorTexture != 0) DisposeTexture();

    glGenTextures(1, &_colorTexture);
    glBindTexture(GL_TEXTURE_2D, _colorTexture);

    glTexImage2D(GL_TEXTURE_2D, 0, GL_RGBA, _outputWidth, _outputHeight, 0, GL_RGBA, GL_UNSIGNED_BYTE, nullptr);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_NEAREST);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_NEAREST);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);

    // attach texture to color output
    glFramebufferTexture2D(GL_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, GL_TEXTURE_2D, _colorTexture, 0);

    glBindTexture(GL_TEXTURE_2D, 0); // Reset texture binding
}

void rei::render::FrameBuffer::DisposeTexture()
{
    glDeleteTextures(1, &_colorTexture);
    _colorTexture = -1;
}
