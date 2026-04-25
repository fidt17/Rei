#pragma once

#define REI_API __declspec(dllexport)
#define REI_EXTERN_API extern "C" REI_API

#define STRING(x) std::to_string(x)

#define SERIALIZE
#define HIDE_IN_EDITOR
#define REQUIRE_COMPONENT(COMPONENT_NAME)
#define SERIALIZABLE_BODY(CLASS_NAME)\
    public:\
    CLASS_NAME() = default;\
    CLASS_NAME& operator=(const CLASS_NAME& other) = default;\
    nlohmann::json REI_GET() const;\
    void REI_SET(const nlohmann::json& data); \
    void ResolveDependencies(); \

#define BEHAVIOUR_BODY(BEHAVIOUR_NAME)\
    public:\
    BEHAVIOUR_NAME() = default;\
    explicit BEHAVIOUR_NAME(const i32 id, const rei::ecs::Entity entity) : Behaviour(id, entity) {} ;\
    BEHAVIOUR_NAME& operator=(const BEHAVIOUR_NAME& other) = default;\
    nlohmann::json REI_GET() const; \
    void REI_SET(const nlohmann::json& data); \
    void ResolveDependencies(); \
    private:

#define REI_EVENT(x) eventpp::CallbackList<void(x)>
#define REI_EVENT_HANDLE(x) eventpp::internal_::CallbackListBase<void(x), eventpp::DefaultPolicies>::Handle

#define SERIALIZABLE_ENUM(ENUM_NAME) enum ENUM_NAME

// --- RENDER ---
#define REI_MAX_POINT_LIGHTS_COUNT 4

#define SORTING_ORDER_DEFAULT 1000
#define SORTING_ORDER_POST_PROCESSING 2000
#define SORTING_ORDER_MAX_VALUE 10000

// --------------

#include "pch.h"
#include "Common/Logging/Log.h"
#include "Common/Assert.h"
#include "Common/ExitCodes.h"
#include "Modules/Assets/Core/AssetIds.h"

#include "glm/ext/matrix_transform.hpp"

#include "Ecs/Ecs.h"

#include "Modules/Assets/Core/AssetManager.h"
#include "Modules/Behaviour/Behaviour.h"

// Force include components (needed for ecs component registry to initialize correctly across different DLLs)
#include "Modules/Editor/Components/SelectedTag.h"
