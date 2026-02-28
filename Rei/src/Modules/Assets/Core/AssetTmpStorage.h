#pragma once

#include <mutex>
#include <string>
#include <vector>

namespace rei::assets
{
    class AssetTmpStorage
    {
    public:
        REI_API std::string CreateTempPath(const std::string& sourcePath);
        REI_API void DeleteAll();

    private:
        std::mutex _mutex;
        std::vector<std::string> _files = {};
        u32 _counter = 0;
    };
}
