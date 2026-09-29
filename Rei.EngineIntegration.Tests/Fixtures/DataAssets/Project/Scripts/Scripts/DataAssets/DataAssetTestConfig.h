#pragma once

#include <string>
#include <vector>

#include <Core.h>
#include <Common/Math/Vector2.h>
#include <Common/Math/Vector3.h>
#include <Modules/Assets/Core/AssetRef.h>
#include <Modules/Render/Color/Color.h>
#include <Modules/Render/Material/Material.h>

#include "DataAssetTestDependency.h"
#include "DataAssetTestMode.h"

namespace symbols::testing
{
    class DataAssetTestConfig
    {
        DATA_ASSET_BODY(DataAssetTestConfig)

        SERIALIZE bool _enabled;
        SERIALIZE i32 _signedValue;
        SERIALIZE u32 _unsignedValue;
        SERIALIZE f32 _floatValue;
        SERIALIZE std::string _label;
        SERIALIZE DataAssetTestMode _mode;
        SERIALIZE rei::math::Vector2 _position2D;
        SERIALIZE rei::math::Vector3 _position3D;
        SERIALIZE rei::render::Color _tint;
        SERIALIZE std::vector<f32> _weights;
        SERIALIZE rei::assets::AssetRef<rei::render::Material> _material;
        SERIALIZE rei::assets::AssetRef<DataAssetTestDependency> _dependency;

    public:
        bool IsEnabled() const
        {
            return _enabled;
        }

        f32 GetFloatValue() const
        {
            return _floatValue;
        }

        const rei::assets::AssetRef<rei::render::Material>& GetMaterial() const
        {
            return _material;
        }

        const rei::assets::AssetRef<DataAssetTestDependency>& GetDependency() const
        {
            return _dependency;
        }
    };
}
