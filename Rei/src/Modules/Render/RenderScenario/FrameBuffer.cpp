#include "FrameBuffer.h"

#include "glad/glad.h"

rei::render::FrameBuffer::FrameBuffer(const i32 width, const i32 height)
    :
    _outputWidth(width),
    _outputHeight(height)
{
    glGenFramebuffers(1, &_fbo);
    glBindFramebuffer(GL_FRAMEBUFFER, _fbo);

    CreateTextures();
}

rei::render::FrameBuffer::~FrameBuffer()
{
    glDeleteFramebuffers(1, &_fbo);
    DisposeTextures();
}

void rei::render::FrameBuffer::EnableBuffer(const i32 width, const i32 height)
{
    glBindFramebuffer(GL_FRAMEBUFFER, _fbo);

    const bool updateTexture = _outputWidth != width || _outputHeight != height;
    _outputWidth = width;
    _outputHeight = height;

    if (updateTexture)
    {
        CreateTextures();
    }

    if (glCheckFramebufferStatus(GL_FRAMEBUFFER) != GL_FRAMEBUFFER_COMPLETE)
    {
        LOG_ERROR("FrameBuffer setup is not complete")
    }
}

u32 rei::render::FrameBuffer::GetColorTexture() const
{
    return _colorTexture;
}

void rei::render::FrameBuffer::CreateTextures()
{
    if (_outputWidth == 0 || _outputHeight == 0) return;

    DisposeTextures();

    // Configure color texture
    glGenTextures(1, &_colorTexture);
    glBindTexture(GL_TEXTURE_2D, _colorTexture);

    glTexImage2D(GL_TEXTURE_2D, 0, GL_RGBA, _outputWidth, _outputHeight, 0, GL_RGBA, GL_UNSIGNED_BYTE, nullptr);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_NEAREST);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_NEAREST);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);
    
    glFramebufferTexture2D(GL_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, GL_TEXTURE_2D, _colorTexture, 0);

    // Configure depth-stencil render buffer
    glGenRenderbuffers(1, &_rbo);
    glBindRenderbuffer(GL_RENDERBUFFER, _rbo);
    glRenderbufferStorage(GL_RENDERBUFFER, GL_DEPTH24_STENCIL8, _outputWidth, _outputHeight);
    glBindRenderbuffer(GL_RENDERBUFFER, 0);
    glFramebufferRenderbuffer(GL_FRAMEBUFFER, GL_DEPTH_STENCIL_ATTACHMENT, GL_RENDERBUFFER, _rbo);

    glBindTexture(GL_TEXTURE_2D, 0); // Reset texture binding
}

void rei::render::FrameBuffer::DisposeTextures() const
{
    glDeleteTextures(1, &_colorTexture);
    glDeleteRenderbuffers(1, &_rbo);
}
