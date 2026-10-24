#include "pch.h"
#include "Common/Profiling/ProfileMarkers.h"
#include "Common/Time/Stopwatch.h"
#include "Texture.h"

#include "stb_image.h"
#include "glad/glad.h"
#include <utility>

rei::render::Texture::Texture(resources::BinaryReader& reader)
{
    rei::time::Stopwatch timer;
    timer.Start();
    _width = reader.GetI32();
    _height = reader.GetI32();
    _format = reader.GetI32();
    // Sized RGB/RGBA formats explicitly mark linear data; legacy RGB/RGBA packs contain sRGB colors.
    if (_format == GL_RGB8 || _format == GL_RGBA8) _colorSpace = TextureColorSpace::Linear;

    _rawData = reader.GetVector<u8>();
    const auto totalMs = timer.ElapsedMs();
    if (rei::profiling::IsLoadTimingEnabled()) rei::profiling::WriteLoadTiming(nlohmann::json({
        {"stage", "texture.read"}, {"width", _width}, {"height", _height}, {"bytes", _rawData.size()},
        {"readMs", totalMs}, {"totalMs", totalMs}
    }).dump());
}

rei::render::Texture::Texture(const i32 width, const i32 height, const i32 format, std::vector<u8> rawData, const TextureColorSpace colorSpace)
    : _width(width),
      _height(height),
      _format(format),
      _colorSpace(colorSpace),
      _rawData(std::move(rawData))
{
}

rei::render::Texture::Texture(Texture&& other) noexcept
    : _id(std::exchange(other._id, 0)),
      _width(other._width),
      _height(other._height),
      _format(other._format),
      _colorSpace(other._colorSpace),
      _rawData(std::move(other._rawData)),
      _textureTag(std::move(other._textureTag)),
      _type(other._type)
{
}

rei::render::Texture& rei::render::Texture::operator=(Texture&& other) noexcept
{
    if (this == &other) return *this;
    ReleaseTexture();
    _id = std::exchange(other._id, 0);
    _width = other._width;
    _height = other._height;
    _format = other._format;
    _colorSpace = other._colorSpace;
    _rawData = std::move(other._rawData);
    _textureTag = std::move(other._textureTag);
    _type = other._type;
    return *this;
}

rei::render::Texture::~Texture()
{
    ReleaseTexture();
}

void rei::render::Texture::ReleaseTexture()
{
    if (_id == 0) return;
    rei::time::Stopwatch timer;
    timer.Start();
    glDeleteTextures(1, &_id);
    _id = 0;
    const auto totalMs = timer.ElapsedMs();
    if (rei::profiling::IsLoadTimingEnabled()) rei::profiling::WriteLoadTiming(nlohmann::json({
        {"stage", "texture.release"}, {"width", _width}, {"height", _height}, {"totalMs", totalMs}
    }).dump());
}

void rei::render::Texture::PostLoad()
{
    if (_id != 0)
    {
        return;
    }

    rei::time::Stopwatch timer;
    timer.Start();
    glGenTextures(1, &_id);
    glBindTexture(GL_TEXTURE_2D, _id);

    const auto format = _format == GL_RGB8 ? GL_RGB : _format == GL_RGBA8 ? GL_RGBA : _format;
    const auto internalFormat = format == GL_RGB ? (_colorSpace == TextureColorSpace::Srgb ? GL_SRGB8 : GL_RGB8)
        : format == GL_RGBA ? (_colorSpace == TextureColorSpace::Srgb ? GL_SRGB8_ALPHA8 : GL_RGBA8) : format;
    i32 unpackAlignment = 0;
    glGetIntegerv(GL_UNPACK_ALIGNMENT, &unpackAlignment);
    glPixelStorei(GL_UNPACK_ALIGNMENT, 1);
    glTexImage2D(GL_TEXTURE_2D, 0, internalFormat, _width, _height, 0, format, GL_UNSIGNED_BYTE, _rawData.data());
    glPixelStorei(GL_UNPACK_ALIGNMENT, unpackAlignment);
    const auto uploadMs = timer.ElapsedMs();
    glGenerateMipmap(GL_TEXTURE_2D);
    const auto mipmapMs = timer.ElapsedMs();

    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_REPEAT);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_REPEAT);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR_MIPMAP_LINEAR);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);

    _rawData.clear();
    _rawData.shrink_to_fit();
    const auto totalMs = timer.ElapsedMs();
    if (rei::profiling::IsLoadTimingEnabled()) rei::profiling::WriteLoadTiming(nlohmann::json({
        {"stage", "texture.gpu"}, {"width", _width}, {"height", _height},
        {"uploadMs", uploadMs}, {"mipmapMs", mipmapMs - uploadMs}, {"cleanupMs", totalMs - mipmapMs}, {"totalMs", totalMs}
    }).dump());
}

void rei::render::Texture::Use(const i32 idx) const
{
    profiling::Count(profiling::markers::TEXTURES.Id);
    glActiveTexture(GL_TEXTURE0 + idx);
    glBindTexture(GL_TEXTURE_2D, _id);
}

u32 rei::render::Texture::GetId() const
{
    return _id;
}

i32 rei::render::Texture::GetWidth() const
{
    return _width;
}

i32 rei::render::Texture::GetHeight() const
{
    return _height;
}

rei::render::TextureType rei::render::Texture::GetType() const
{
    return _type;
}

void rei::render::Texture::SetType(const TextureType type)
{
    _type = type;
}

std::string rei::render::Texture::GetTag() const
{
    return _textureTag;
}
