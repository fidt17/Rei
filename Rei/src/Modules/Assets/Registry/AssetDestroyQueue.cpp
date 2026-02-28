#include "pch.h"
#include "AssetDestroyQueue.h"

namespace rei::assets
{
    void AssetDestroyQueue::Enqueue(const std::shared_ptr<AssetRecord>& record)
    {
        if (record == nullptr) return;

        std::scoped_lock lock(_queueMutex);
        _queue.push_back(record);
    }

    void AssetDestroyQueue::Flush()
    {
        std::vector<std::shared_ptr<AssetRecord>> recordsToDestroy;
        {
            // Move queued records out under a lock, then release them outside the lock
            // to avoid running destructors while holding _queueMutex.
            std::scoped_lock lock(_queueMutex);
            recordsToDestroy.swap(_queue);
        }

        recordsToDestroy.clear();
    }

    i32 AssetDestroyQueue::Size() const
    {
        std::scoped_lock lock(_queueMutex);
        return static_cast<i32>(_queue.size());
    }
}

