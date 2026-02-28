#pragma once

#include <string>

#include "Common/Primitives.h"
#include "Modules/Resources/Serialization/BinaryReader.h"

namespace rei::assets
{
    template <typename T>
    T ReadAssetFromBinary(const std::string& path, const i64 offset, i64& size)
    {
        auto reader = resources::BinaryReader(path, offset);
        auto asset = reader.Get<T>();

        size = reader.GetPosition() - offset;
        reader.Close();

        return asset;
    }

    template <typename T>
    T ReadAssetFromBinary(const std::string& path, const i64 offset)
    {
        i64 size = 0;
        return ReadAssetFromBinary<T>(path, offset, size);
    }
}
