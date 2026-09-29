#pragma once

#include <Core.h>
#include <Modules/Assets/Core/AssetRef.h>

#include "DataAssetTestConfig.h"

namespace symbols::testing
{
    class DataAssetConsumerBehaviour final : public rei::Behaviour
    {
        BEHAVIOUR_BODY(DataAssetConsumerBehaviour)

        SERIALIZE rei::assets::AssetRef<DataAssetTestConfig> _config;

        bool _hasObservedValue = false;
        f32 _lastObservedValue = 0.0f;

    public:
        void Update() override
        {
            if (!_config.IsLoaded()) return;

            const auto value = _config->GetFloatValue();
            if (_hasObservedValue && value == _lastObservedValue) return;

            _hasObservedValue = true;
            _lastObservedValue = value;
            LOG(
                "DataAsset test observed float={}, materialLoaded={}, dependencyLoaded={}",
                value,
                _config->GetMaterial().IsLoaded(),
                _config->GetDependency().IsLoaded())
        }
    };
}
