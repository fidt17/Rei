#include "pch.h"
#include "TextAsset.h"

rei::assets::TextAsset::TextAsset(resources::BinaryReader& reader)
{
    _value = reader.GetStr();
}

const std::string& rei::assets::TextAsset::GetValue() const
{
    return _value;
}
