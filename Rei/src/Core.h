#pragma once

#define REI_API __declspec(dllexport)
#define REI_EXTERN_API extern "C" REI_API

#define STRING(x) std::to_string(x)

#define SERIALIZE
#define SERIALIZABLE_BODY(CLASS_NAME)\
    public:\
    CLASS_NAME() = default;\
    CLASS_NAME& operator=(const CLASS_NAME& other) = default;\
    nlohmann::json REI_GET() const;\
    void REI_SET(const nlohmann::json& data); \

#define BEHAVIOUR_BODY(BEHAVIOUR_NAME)\
    public:\
    BEHAVIOUR_NAME() = default;\
    explicit BEHAVIOUR_NAME(const i32 id, const rei::ecs::Entity entity) : Behaviour(id, entity) {} ;\
    BEHAVIOUR_NAME& operator=(const BEHAVIOUR_NAME& other) = default;\
    nlohmann::json REI_GET() const; \
    void REI_SET(const nlohmann::json& data); \
    private:

#define SERIALIZABLE_ENUM(ENUM_NAME) enum ENUM_NAME

// --- RENDER ---
#define REI_MAX_POINT_LIGHTS_COUNT 4
#define REI_FALLBACK_MATERIAL_ID "REI_ERROR_MATERIAL"
#define REI_LIGHT_SOURCE_MATERIAL_ID "REI_LIGHT_SOURCE_MATERIAL"
#define REI_OUTLINE_MATERIAL_ID "REI_OUTLINE_MATERIAL"
// --------------

#include "pch.h"
#include "Common/Logging/Log.h"
#include "Common/Assert.h"
#include "Common/ExitCodes.h"

#include "glm/ext/matrix_transform.hpp"

#include "Ecs/Ecs.h"

#include "Modules/Assets/AssetManager.h"
#include "Modules/Behaviour/Behaviour.h"

// Force include components
#include "Modules/Editor/SelectedTag.h"
