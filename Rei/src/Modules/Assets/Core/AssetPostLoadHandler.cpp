#include "pch.h"
#include "AssetPostLoadHandler.h"

#include <sstream>
#include <thread>

namespace rei::assets
{
    namespace
    {
        std::string GetCurrentThreadIdString()
        {
            std::ostringstream stream;
            stream << std::this_thread::get_id();
            return stream.str();
        }
    }

    thread_local bool AssetPostLoadHandler::_suppressPostLoadForCurrentThread = false;

    AssetPostLoadHandler::ScopedPostLoadSuppression::ScopedPostLoadSuppression(const bool suppress)
        : _previousState(IsSuppressedForCurrentThread())
    {
        SetSuppressedForCurrentThread(suppress);
    }

    AssetPostLoadHandler::ScopedPostLoadSuppression::~ScopedPostLoadSuppression()
    {
        SetSuppressedForCurrentThread(_previousState);
    }

    void AssetPostLoadHandler::SetSuppressedForCurrentThread(const bool suppress)
    {
        _suppressPostLoadForCurrentThread = suppress;
    }

    bool AssetPostLoadHandler::IsSuppressedForCurrentThread()
    {
        return _suppressPostLoadForCurrentThread;
    }

    void AssetPostLoadHandler::Queue(const std::string& id, DeferredPostLoadAction action)
    {
        std::scoped_lock lock(_deferredPostLoadMutex);
        if (_deferredPostLoadIds.contains(id)) return;

        _deferredPostLoadIds.insert(id);
        _deferredPostLoadActions.push_back(std::move(action));
    }

    bool AssetPostLoadHandler::Flush(const std::function<void(f32)>& onProgress)
    {
        std::vector<DeferredPostLoadAction> deferredActions;
        {
            std::scoped_lock lock(_deferredPostLoadMutex);
            deferredActions.swap(_deferredPostLoadActions);
            _deferredPostLoadIds.clear();
        }

        bool allSucceeded = true;
        u64 completed = 0;
        if (onProgress && deferredActions.empty()) onProgress(1);
        for (const auto& action : deferredActions)
        {
            if (!action()) allSucceeded = false;
            if (onProgress) onProgress(static_cast<f32>(++completed) / deferredActions.size());
        }

        return allSucceeded;
    }
}
