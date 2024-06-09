#pragma once

#define REI_API __declspec(dllexport)
#define REI_EXTERN_API extern "C" REI_API

#define STRING(x) std::to_string(x) 

#include "pch.h"
#include "Common/Logging/Log.h"
#include "Common/Assert.h"
#include "Common/ExitCodes.h"

#include "Ecs/Ecs.h"

#include "Modules/Assets/AssetManager.h"
#include "Modules/Behaviour/Behaviour.h"
