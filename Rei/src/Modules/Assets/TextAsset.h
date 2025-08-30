#pragma once

namespace rei::assets
{
    class TextAsset
    {
    public:
        REI_API TextAsset() = default;
        explicit TextAsset(resources::BinaryReader& reader);

        REI_API const std::string& GetValue() const;

    private:
        std::string _value;
    };
}
