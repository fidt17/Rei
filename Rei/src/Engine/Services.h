#pragma once

namespace rei::window
{
    class WindowManager;
}

namespace rei
{
    namespace render
    {
        class Gizmos;
    }

    namespace api
    {
        class EditorEventsRelay;
    }

    class EntityManager;

    namespace internal::engine
    {
        class Engine;
    }

    class Services
    {
    public:
        Services(Services& other) = delete;
        void operator=(const Services&) = delete;

        void SetEngine(internal::engine::Engine* value) { _engine = value; }
        REI_API internal::engine::Engine& GetEngine() const { return *_engine; }

        void SetInternalWorld(const std::shared_ptr<ecs::World>& world) { _internalWorld = world; }
        REI_API ecs::World& GetInternalWorld() const { return *_internalWorld; }

        void SetEntityManager(const std::shared_ptr<EntityManager>& entityManager) { _entityManager = entityManager; }
        REI_API EntityManager& GetEntityManager() const { return *_entityManager; }

        void SetAssetManager(const std::shared_ptr<assets::AssetManager>& assetManager) { _assetManager = assetManager; }
        REI_API assets::AssetManager& GetAssetManager() const { return *_assetManager; }

        void SetWindowManager(const std::shared_ptr<window::WindowManager>& windowManager) { _windowManager = windowManager; }
        REI_API window::WindowManager& GetWindowManager() const { return *_windowManager; }

        void SetEditorEventsRelay(const std::shared_ptr<api::EditorEventsRelay>& relay) { _editorEventsRelay = relay; }
        REI_API api::EditorEventsRelay& GetEditorEventsRelay() const { return *_editorEventsRelay; }
        
        void SetGizmos(const std::shared_ptr<render::Gizmos>& gizmos) { _gizmos = gizmos; }
        REI_API render::Gizmos& GetGizmos() const { return *_gizmos; }

        REI_API static Services* GetInstance();

    private:
        Services() = default;
        static Services* _instance;

        internal::engine::Engine* _engine;
        std::shared_ptr<ecs::World> _internalWorld;
        std::shared_ptr<EntityManager> _entityManager;
        std::shared_ptr<assets::AssetManager> _assetManager;
        std::shared_ptr<window::WindowManager> _windowManager;
        std::shared_ptr<api::EditorEventsRelay> _editorEventsRelay;
        std::shared_ptr<render::Gizmos> _gizmos;
    };

    inline internal::engine::Engine& GetEngine() { return Services::GetInstance()->GetEngine(); }
    inline ecs::World& GetInternalWorld() { return Services::GetInstance()->GetInternalWorld(); }
    inline EntityManager& GetEntityManager() { return Services::GetInstance()->GetEntityManager(); }
    inline assets::AssetManager& GetAssetManager() { return Services::GetInstance()->GetAssetManager(); }
    inline window::WindowManager& GetWindowManager() { return Services::GetInstance()->GetWindowManager(); }
    inline api::EditorEventsRelay& GetEditorEventsRelay() { return Services::GetInstance()->GetEditorEventsRelay(); }
    inline render::Gizmos& GetGizmos() { return Services::GetInstance()->GetGizmos(); }
}
