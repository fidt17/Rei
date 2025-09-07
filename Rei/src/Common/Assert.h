#pragma once

#ifdef DEBUG
    #define REI_DEBUG_BREAK() __debugbreak();
#else
    #define REI_DEBUG_BREAK()
#endif

#define CODE_PATH std::format("{} at line {}", std::string(__FILE__), std::to_string(__LINE__))

#ifdef DEBUG
    #define REI_ASSERT_S(x) REI_ASSERT(x, "Assertion Failed")
    #define REI_ASSERT_NOT_NULL(x) REI_THROW_IF((x) == nullptr, "Null reference exception \"" + std::string(#x) + "\"")
    #define REI_ASSERT(x, msg) if (!(x)) { auto path = CODE_PATH; LOG_ERROR("{}\n {}", msg, path)}
#else
    #define REI_ASSERT_S(x) 
    #define REI_ASSERT_NOT_NULL(x) 
    #define REI_ASSERT(x, msg) 
#endif

#define REI_THROW_IF(x, msg) if (x) { REI_THROW(msg) }
#define REI_THROW(msg) throw std::runtime_error(std::format("{}\n {}", msg, CODE_PATH));
