#pragma once

#define REI_API __declspec(dllexport)
#define REI_EXTERN_API extern "C" REI_API

#define STRING(x) std::to_string(x)

#define SERIALIZE
#define SERIALIZABLE_BODY(CLASS_NAME)\
    public:\
    CLASS_NAME() = default;\
    explicit CLASS_NAME(const nlohmann::json& data);\
    CLASS_NAME& operator=(const CLASS_NAME& other) = default;\

#include "pch.h"
#include "Common/Logging/Log.h"
#include "Common/Assert.h"
#include "Common/ExitCodes.h"

#include "glm/ext/matrix_transform.hpp"

#include "Ecs/Ecs.h"

#include "Modules/Assets/AssetManager.h"
#include "Modules/Behaviour/Behaviour.h"
