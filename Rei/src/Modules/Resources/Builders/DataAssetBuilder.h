#pragma once
#include "Modules/Resources/Serialization/BinaryWriter.h"

namespace rei::resources
{
    void BuildDataAsset(const std::filesystem::path& assetPath, BinaryWriter& writer);
}
