#include "pch.h"
#include "Services.h"

namespace rei
{
    Services* Services::_instance = nullptr;

    void Services::ReleaseEngineServices(const internal::engine::Engine* engine)
    {
        if (_engine != engine) return;

        // Runtime components release while their other services remain available.
        _gizmos.reset();
        _internalWorld.reset();
        _entityManager.reset();
        _editorEventsRelay.reset();
        _windowManager.reset();
        _assetManager.reset();
        _diagnostics.reset();
        _profiler.reset();
        _time.reset();
        _engine = nullptr;
    }

    void Services::ReleaseGizmos(const std::shared_ptr<render::Gizmos>& gizmos)
    {
        if (_gizmos == gizmos) _gizmos.reset();
    }

    Services* Services::GetInstance()
    {
        if (_instance == nullptr)
        {
            _instance = new Services();
        }
        return _instance;
    }
}
