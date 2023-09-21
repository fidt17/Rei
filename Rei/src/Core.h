#pragma once

#define REI_API __declspec(dllexport)

#define REI_EXTERN_API extern "C" REI_API

#include "pch.h"
#include "Common/Logging/Log.h"
#include "Common/Assert.h"
#include "Common/IFactory.h"
#include "Common/Event.h"
