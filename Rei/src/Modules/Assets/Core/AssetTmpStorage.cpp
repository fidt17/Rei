#include "pch.h"
#include "AssetTmpStorage.h"

namespace rei::assets
{
    std::string AssetTmpStorage::CreateTempPath(const std::string& sourcePath)
    {
        const auto baseFilename = sourcePath.substr(sourcePath.find_last_of("/\\") + 1);
        const auto dirPath = std::filesystem::temp_directory_path().string() + "Rei Engine\\";

        std::scoped_lock lock(_mutex);
        const auto dest = dirPath + baseFilename + "_" + std::to_string(_counter++) + ".data";

        std::filesystem::create_directory(dirPath);
        remove(dest.c_str());

        _files.push_back(dest);
        return dest;
    }

    void AssetTmpStorage::DeleteAll()
    {
        std::vector<std::string> filesToDelete;
        {
            std::scoped_lock lock(_mutex);
            filesToDelete.swap(_files);
        }

        for (const auto& path : filesToDelete)
        {
            remove(path.c_str());
        }
    }
}
