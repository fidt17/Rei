#pragma once

namespace rei::resources
{
    REI_API std::string ReadAllText(const std::filesystem::path& path);
    REI_API i64 BuildAsset(const std::string& file, const std::string& dest, i64 offset);
}
