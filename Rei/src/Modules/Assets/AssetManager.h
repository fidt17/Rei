#pragma once
#include <filesystem>
#include <fstream>

namespace rei::assets
{
    class AssetManager
    {
    public:
        explicit AssetManager(const std::string& resourcesPath)
        {
            current_path(std::filesystem::path(resourcesPath));
        }

        template <typename T>
        bool Load(const std::string& path, T& obj) const
        {
            std::ifstream stream(path, std::ios::in | std::ios::binary);
            if (stream.bad())
            {
                LOG_ERROR("Could not open read stream for " + path);
                return false;
            }

            const std::vector<u8> buffer(std::istreambuf_iterator(stream), {});
            if (buffer.size() != sizeof(T))
            {
                LOG_ERROR("Buffer[" + std::to_string(buffer.size()) + "] size does not equal size of desired object [" + std::to_string(sizeof(T)) + "]");
                return false;
            }
            const void* location = buffer.data();

            memcpy(&obj, location, sizeof(T));

            return true;
        }
    };
}
