#pragma once

#include <functional>
#include <mutex>
#include <string>
#include <unordered_set>
#include <vector>

namespace rei::assets
{
    class AssetPostLoadHandler
    {
    public:
        using DeferredPostLoadAction = std::function<bool()>;
        
        class ScopedPostLoadSuppression
        {
        public:
            explicit ScopedPostLoadSuppression(bool suppress);
            ~ScopedPostLoadSuppression();
            
            ScopedPostLoadSuppression(const ScopedPostLoadSuppression&) = delete;
            ScopedPostLoadSuppression& operator=(const ScopedPostLoadSuppression&) = delete;
            ScopedPostLoadSuppression(ScopedPostLoadSuppression&&) = delete;
            ScopedPostLoadSuppression& operator=(ScopedPostLoadSuppression&&) = delete;

        private:
            bool _previousState = false;
        };

        REI_API static void SetSuppressedForCurrentThread(bool suppress);
        REI_API static bool IsSuppressedForCurrentThread();

        REI_API void Queue(const std::string& id, DeferredPostLoadAction action);
        REI_API bool Flush();

    private:
        mutable std::mutex _deferredPostLoadMutex;
        std::unordered_set<std::string> _deferredPostLoadIds;
        std::vector<DeferredPostLoadAction> _deferredPostLoadActions;
        static thread_local bool _suppressPostLoadForCurrentThread;
    };
}
