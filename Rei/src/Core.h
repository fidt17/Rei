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
    nlohmann::json REI_GET() const;\

#define BEHAVIOUR_BODY(BEHAVIOUR_NAME)\
    public:\
    BEHAVIOUR_NAME() = default;\
    explicit BEHAVIOUR_NAME(const i32 id, const rei::ecs::Entity entity) : Behaviour(id, entity) {} ;\
    explicit BEHAVIOUR_NAME(const i32 id, const rei::ecs::Entity entity, const nlohmann::json& data);\
    BEHAVIOUR_NAME& operator=(const BEHAVIOUR_NAME& other) = default;\
    nlohmann::json REI_GET() const; \
    private:

#include "pch.h"
#include "Common/Logging/Log.h"
#include "Common/Assert.h"
#include "Common/ExitCodes.h"

#include "glm/ext/matrix_transform.hpp"

#include "Ecs/Ecs.h"

#include "Modules/Assets/AssetManager.h"
#include "Modules/Behaviour/Behaviour.h"
