#pragma once

#include <memory>
#include <mutex>
#include <vector>

#include "Common/Primitives.h"
#include "AssetRecord.h"

namespace rei::assets
{
    class AssetDestroyQueue
    {
    public:
        void Enqueue(const std::shared_ptr<AssetRecord>& record);
        void Flush();
        i32 Size() const;

    private:
        mutable std::mutex _queueMutex;
        std::vector<std::shared_ptr<AssetRecord>> _queue = {};
    };
}
