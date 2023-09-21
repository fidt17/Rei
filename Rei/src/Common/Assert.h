#pragma once

#ifdef DEBUG
    #define REI_DEBUG_BREAK() __debugbreak();
#else
    #define REI_DEBUG_BREAK()
#endif

#ifdef DEBUG
    #define REI_ASSERT_S(x) REI_ASSERT(x, "Assertion Failed")
    #define REI_ASSERT(x, msg) if (!(x)) { LOG_ERROR((msg), std::string(__FILE__) + std::string(" at line ") + std::to_string(__LINE__)) }
#else
    #define REI_ASSERT(...)
    #define REI_ASSERT_M(x, msg)
#endif

#define REI_THROW_IF(x, msg) if (x) { REI_THROW(msg); }
#define REI_THROW(msg) LOG_ERROR((msg), std::string(__FILE__) + std::string(" at line ") + std::to_string(__LINE__)); throw std::exception(msg);
