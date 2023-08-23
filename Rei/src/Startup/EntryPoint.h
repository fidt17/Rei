#pragma once

#include "App.h"

namespace rei
{
    REI_EXTERN_API void StartEngine();
    REI_EXTERN_API int StopEngine(int exitCode);
    
    REI_EXTERN_API App* GetApp();
}

/*
inline int main()
{
    rei::StartEngine(nullptr);
    return 0;
}
*/
