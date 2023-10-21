#pragma once

namespace rei
{
    namespace resources
    {
        class BinaryReader;
    }
}

namespace rei::assets
{
    struct AssetId
    {
        const std::string Id;

        REI_API explicit AssetId(std::string str);
        REI_API explicit AssetId(resources::BinaryReader& reader);
    };
}
