#include "pch.h"
#include "support/NativeEngineFixture.h"
#include "ProjectDll/NativeProjectJournal.h"
#include <array>
#include <chrono>

using namespace rei;
using namespace rei::tests;

namespace
{
    constexpr size_t JOURNAL_EVENTS = static_cast<size_t>(ProjectJournalEvent::Count);

    struct SessionJournal
    {
        std::array<std::atomic<i32>, JOURNAL_EVENTS> Events{};
        std::atomic<i32> ThreadsStarted = 0;
        std::atomic<i32> ThreadsJoined = 0;
        std::atomic<i32> EnginesDestroyed = 0;
        std::atomic<i32> WorkerErrorsCopied = 0;
        std::atomic<bool> CancelRequested = false;
        std::mutex Mutex;
        std::condition_variable Changed;
        bool Ready = false;

        i32 Count(const ProjectJournalEvent event) const { return Events[static_cast<size_t>(event)].load(); }

        static void Record(void* context, const i32 event)
        {
            auto& journal = *static_cast<SessionJournal*>(context);
            if (event < 0 || static_cast<size_t>(event) >= JOURNAL_EVENTS) return;
            ++journal.Events[static_cast<size_t>(event)];
            std::scoped_lock lock(journal.Mutex);
            if (event == static_cast<i32>(ProjectJournalEvent::Ready)) journal.Ready = true;
            journal.Changed.notify_all();
        }

        static bool IsCancelled(void* context) { return static_cast<SessionJournal*>(context)->CancelRequested.load(); }
    };

    class NativeProjectSession
    {
        struct WorkerState
        {
            std::shared_ptr<SessionJournal> Journal;
            void* Engine = nullptr;
            bool Created = false;
            bool Finished = false;
            std::string ErrorMessage;
        };
        struct PinnedSession { HMODULE Module; std::shared_ptr<WorkerState> State; };

    public:
        explicit NativeProjectSession(const i32 version, const TemporaryDirectory& resources, const i32 failureMode = 0, const char* requiredExport = nullptr, std::shared_ptr<SessionJournal> journal = {}, const i32 creationTimeoutMs = 10000, const i32 readinessTimeoutMs = 10000)
            : Journal(journal ? std::move(journal) : std::make_shared<SessionJournal>()), _state(std::make_shared<WorkerState>())
        {
            _state->Journal = Journal;
            std::string startupError;
            try
            {
                const auto directory = ExecutablePath().parent_path().parent_path() / "NativeProjectDll";
                const auto dll = directory / ("NativeProjectV" + std::to_string(version) + ".dll");
                if (!std::filesystem::exists(dll)) throw std::runtime_error("DLL prerequisite unavailable: build tests/ProjectDll/NativeProject.vcxproj for versions 1 and 2");
                _module = LoadLibraryExW(dll.c_str(), nullptr, LOAD_WITH_ALTERED_SEARCH_PATH);
                if (!_module) throw std::runtime_error("Native project LoadLibrary failed: " + std::to_string(GetLastError()));
                _create = Symbol<void* (*)(const char*, i32)>("NativeProjectCreateEngine");
                _start = Symbol<void (*)(void*)>("NativeProjectStart");
                _shutdown = Symbol<i32 (*)(void*, i32)>("Shutdown");
                _destroy = Symbol<void (*)(void*)>("DestroyEngine");
                _window = Symbol<bool (*)(void*)>("NativeProjectCreateHiddenWindow");
                _worldAlive = Symbol<bool (*)()>("NativeProjectWorldAlive");
                _resetInstrumentation = Symbol<void (*)()>("NativeProjectResetInstrumentation");
                Read = Symbol<i32 (*)()>("NativeProjectRead");
                LoadSaved = Symbol<i32 (*)()>("NativeProjectLoadSaved");
                SetData = Symbol<bool (*)(const char*, const char*, const char*)>("SetAssetData");
                GetData = Symbol<bool (*)(const char*, const char*, char*, i32)>("GetAssetData");
                if (Symbol<i32 (*)()>("NativeProjectVersion")() != version) throw std::runtime_error("Native project version mismatch");
                if (requiredExport) Symbol<void (*)()>(requiredExport);
                Symbol<void (*)(ProjectJournalCallback, ProjectCancelCallback, void*, i32)>("NativeProjectConfigureJournal")(SessionJournal::Record, SessionJournal::IsCancelled, Journal.get(), failureMode);
                const auto resourceDirectory = resources.File("").string();
                const auto create = _create;
                const auto start = _start;
                const auto createWindow = _window;
                // Worker owns shared state, never a partially constructed session.
                _thread = std::thread([state = _state, resourceDirectory, create, start, createWindow]
                {
                    ++state->Journal->ThreadsStarted;
                    try
                    {
                        if (!state->Journal->CancelRequested)
                        {
                            const auto engine = create(resourceDirectory.c_str(), internal::engine::PlayMode);
                            {
                                std::scoped_lock lock(state->Journal->Mutex);
                                state->Engine = engine;
                                state->Created = true;
                                state->Journal->Changed.notify_all();
                            }
                            if (engine && !state->Journal->CancelRequested)
                            {
                                if (!createWindow(engine)) throw std::runtime_error("Native project window creation failed");
                                if (!state->Journal->CancelRequested) start(engine);
                            }
                        }
                    }
                    catch (const std::exception& error)
                    {
                        std::scoped_lock lock(state->Journal->Mutex);
                        state->ErrorMessage = error.what();
                        ++state->Journal->WorkerErrorsCopied;
                    }
                    catch (...)
                    {
                        std::scoped_lock lock(state->Journal->Mutex);
                        state->ErrorMessage = "Unknown native project worker exception";
                        ++state->Journal->WorkerErrorsCopied;
                    }
                    std::scoped_lock lock(state->Journal->Mutex);
                    state->Finished = true;
                    state->Journal->Changed.notify_all();
                });
                {
                    std::unique_lock lock(Journal->Mutex);
                    if (failureMode == 3 && !Journal->Changed.wait_for(lock, std::chrono::seconds(5), [&] { return Journal->Count(ProjectJournalEvent::CreationEntered) || _state->Finished; })) throw std::runtime_error("Native creation injection did not reach barrier");
                    if (!Journal->Changed.wait_for(lock, std::chrono::milliseconds(creationTimeoutMs), [&] { return _state->Created || _state->Finished; })) throw std::runtime_error("Native project creation timeout");
                    if (!_state->ErrorMessage.empty()) throw std::runtime_error(_state->ErrorMessage);
                    if (!_state->Engine) throw std::runtime_error("Native project CreateEngine returned null");
                }
                std::unique_lock lock(Journal->Mutex);
                if (failureMode == 5 && !Journal->Changed.wait_for(lock, std::chrono::seconds(5), [&] { return Journal->Count(ProjectJournalEvent::StartEntered) || _state->Finished; })) throw std::runtime_error("Native startup injection did not reach barrier");
                if (!Journal->Changed.wait_for(lock, std::chrono::milliseconds(readinessTimeoutMs), [&] { return Journal->Ready || _state->Finished; })) throw std::runtime_error("Native project readiness timeout");
                if (!_state->ErrorMessage.empty()) throw std::runtime_error(_state->ErrorMessage);
                if (!Journal->Ready) throw std::runtime_error("Native project exited before readiness");
            }
            catch (const std::exception& error) { startupError = error.what(); }
            catch (...) { startupError = "Unknown native project startup exception"; }
            // Original DLL exception has been destroyed before cleanup/unload.
            if (!startupError.empty())
            {
                try { Close(); } catch (const std::exception& error) { startupError += "; cleanup: " + std::string(error.what()); }
                throw std::runtime_error(startupError);
            }
        }

        ~NativeProjectSession() { try { Close(); } catch (...) {} }

        bool Close()
        {
            if (_closed) return _unloaded;
            Journal->CancelRequested = true;
            void* engine = nullptr;
            { std::scoped_lock lock(Journal->Mutex); engine = _state->Engine; }
            std::string shutdownError;
            try { if (engine && _shutdown) _shutdown(engine, 0); }
            catch (const std::exception& error) { shutdownError = error.what(); }
            catch (...) { shutdownError = "Unknown native project shutdown exception"; }
            // Null CreateEngine result still owns a joinable worker.
            if (_thread.joinable()) { _thread.join(); ++Journal->ThreadsJoined; }
            std::string workerError;
            {
                std::scoped_lock lock(Journal->Mutex);
                // Publication may have happened after Close's first engine read.
                engine = _state->Engine;
                workerError = std::move(_state->ErrorMessage);
                _state->ErrorMessage.clear();
            }
            if (engine && _destroy) { _destroy(engine); ++Journal->EnginesDestroyed; }
            { std::scoped_lock lock(Journal->Mutex); _state->Engine = nullptr; }
            // Snapshot happens after Shutdown + join + DestroyEngine, while every
            // DLL function/destructor/callback still has mapped executable code.
            for (size_t i = 0; i < JOURNAL_EVENTS; ++i) TeardownSnapshot[i] = Journal->Events[i].load();
            WorldStillAlive = _worldAlive && _worldAlive();
            const auto balanced = [&](const ProjectJournalEvent created, const ProjectJournalEvent destroyed)
            {
                return TeardownSnapshot[static_cast<size_t>(created)] == TeardownSnapshot[static_cast<size_t>(destroyed)];
            };
            const bool safeToUnload = !WorldStillAlive
                && balanced(ProjectJournalEvent::AssetCreated, ProjectJournalEvent::AssetDestroyed)
                && balanced(ProjectJournalEvent::BehaviourCreated, ProjectJournalEvent::BehaviourDisposed)
                && balanced(ProjectJournalEvent::AppCreated, ProjectJournalEvent::AppDestroyed)
                && balanced(ProjectJournalEvent::CallbackCreated, ProjectJournalEvent::CallbackDestroyed);
            if (_resetInstrumentation) _resetInstrumentation();
            if (_module)
            {
                if (safeToUnload) _unloaded = FreeLibrary(_module) != FALSE;
                else
                {
                    // Preserve unsafe DLL and callback context until subprocess
                    // exit. Pinning is reported as failed unload, never success.
                    static auto* pinned = new std::vector<PinnedSession>();
                    pinned->push_back({_module, _state});
                    ModulePinned = true;
                }
                _module = nullptr;
            }
            else _unloaded = true;
            _closed = true;
            if (!workerError.empty()) throw std::runtime_error(workerError);
            if (!shutdownError.empty()) throw std::runtime_error(shutdownError);
            return _unloaded;
        }

        std::shared_ptr<SessionJournal> Journal;
        std::array<i32, JOURNAL_EVENTS> TeardownSnapshot{};
        bool WorldStillAlive = false;
        bool ModulePinned = false;
        i32 (*Read)() = nullptr;
        i32 (*LoadSaved)() = nullptr;
        bool (*SetData)(const char*, const char*, const char*) = nullptr;
        bool (*GetData)(const char*, const char*, char*, i32) = nullptr;

    private:
        template <typename T> T Symbol(const char* name)
        {
            const auto value = GetProcAddress(_module, name);
            if (!value) throw std::runtime_error(std::string("Missing native test DLL export: ") + name);
            return reinterpret_cast<T>(value);
        }
        std::shared_ptr<WorkerState> _state;
        HMODULE _module = nullptr;
        std::thread _thread;
        bool _closed = false;
        bool _unloaded = false;
        void* (*_create)(const char*, i32) = nullptr;
        void (*_start)(void*) = nullptr;
        i32 (*_shutdown)(void*, i32) = nullptr;
        void (*_destroy)(void*) = nullptr;
        bool (*_window)(void*) = nullptr;
        bool (*_worldAlive)() = nullptr;
        void (*_resetInstrumentation)() = nullptr;
    };
}

TEST_CASE("DLL-01 Project DLL starts reads native asset destroys engine then unloads", "[native][engine-integration][project-dll][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        BehaviourFixture resources([](TemporaryDirectory& files) { PrepareEngineResources(files); });
        NativeProjectSession session(1, resources.Files);
        CHECK(session.Read() == 1);
        CHECK(session.Close());
        CHECK(GetModuleHandleW(L"NativeProjectV1.dll") == nullptr);
    }, 25000, 1536);
}

TEST_CASE("DLL-01 Reload v1 to v2 observes new native project code and serialized value", "[native][engine-integration][project-dll][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        BehaviourFixture resources([](TemporaryDirectory& files) { PrepareEngineResources(files); });
        {
            NativeProjectSession first(1, resources.Files);
            REQUIRE(first.Read() == 1);
            REQUIRE(first.SetData("project-probe", "DataAsset", "{\"Number\":{\"Value\":91}}"));
            CHECK(first.Read() == 91);
        }
        // Keep unload failure visible, then exercise actual v2 while unsafe v1
        // remains mapped by fixture pinning.
        CHECK(GetModuleHandleW(L"NativeProjectV1.dll") == nullptr);
        NativeProjectSession second(2, resources.Files);
        CHECK(second.Read() == 2);
        char output[4096]{};
        REQUIRE(second.GetData("project-probe", "DataAsset", output, 4096));
        CHECK(nlohmann::json::parse(output).at("Number") == 2);
        CHECK(second.Close());
    }, 30000, 1536);
}

TEST_CASE("PERSIST-01 Native generated asset values survive pack write and new DLL engine session", "[native][engine-integration][project-dll][persistence][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        BehaviourFixture resources([](TemporaryDirectory& files) { PrepareEngineResources(files); });
        nlohmann::json saved;
        {
            NativeProjectSession first(1, resources.Files);
            REQUIRE(first.SetData("project-probe", "DataAsset", "{\"Number\":{\"Value\":12345},\"Label\":{\"Value\":\"saved-value\"}}"));
            CHECK(first.Read() == 12345);
            char output[4096]{};
            REQUIRE(first.GetData("project-probe", "DataAsset", output, 4096));
            saved = nlohmann::json::parse(output);
        }
        // REI_GET returns flat native values; packages use the generated loader
        // envelope and Value-wrapped fields, as the actual DataAsset format does.
        const auto persisted = nlohmann::json{{"DataAssetTypeId", 7201}, {"SerializedData", {
            {"Number", {{"Value", saved.at("Number")}}}, {"Label", {{"Value", saved.at("Label")}}},
            {"Settings", {{"Value", {{"Value", {{"Value", saved.at("Settings").at("Value")}}},
                {"Label", {{"Value", saved.at("Settings").at("Label")}}}}}}},
            {"Values", {{"Value", saved.at("Values")}}}
        }}};
        const auto serialized = JsonAsset(persisted);
        const auto packPath = resources.Files.File("persisted.bin");
        resources.Files.Write("persisted.bin", serialized);
        CHECK(ReadBytes(packPath) == serialized);
        auto map = ReadBytes(resources.Files.File("map.bin"));
        u32 count = 0;
        for (u32 i = 0; i < 4; ++i) count |= static_cast<u32>(map[i]) << (8 * i);
        ++count;
        for (u32 i = 0; i < 4; ++i) map[i] = static_cast<u8>(count >> (8 * i));
        AppendString(map, "persisted-probe");
        AppendString(map, "persisted");
        AppendString(map, "persisted.bin");
        AppendInteger(map, 0, 8);
        resources.Files.Write("map.bin", map);
        NativeProjectSession second(2, resources.Files);
        REQUIRE(second.LoadSaved() == 12345);
        char output[4096]{};
        REQUIRE(second.GetData("persisted-probe", "DataAsset", output, 4096));
        const auto restored = nlohmann::json::parse(output);
        CHECK(restored == saved);
        CHECK(restored.at("Label") == "saved-value");
    }, 30000, 1536);
}

TEST_CASE("DLLFIX01 missing export releases module before any worker starts", "[native][engine-integration][project-dll][coverage][coverage-remaining][fixture][isolated]")
{
    Isolated([]
    {
        BehaviourFixture resources([](TemporaryDirectory& files) { PrepareEngineResources(files); });
        auto journal = std::make_shared<SessionJournal>();
        CHECK_THROWS_WITH(NativeProjectSession(1, resources.Files, 0, "NativeProjectMissingExport", journal), "Missing native test DLL export: NativeProjectMissingExport");
        CHECK(journal->ThreadsStarted == 0);
        CHECK(journal->ThreadsJoined == 0);
        CHECK(journal->EnginesDestroyed == 0);
        CHECK(GetModuleHandleW(L"NativeProjectV1.dll") == nullptr);
    }, 25000, 1536);
}

TEST_CASE("DLLFIX02 null CreateEngine result joins worker and releases module", "[native][engine-integration][project-dll][coverage][coverage-remaining][fixture][isolated]")
{
    Isolated([]
    {
        BehaviourFixture resources([](TemporaryDirectory& files) { PrepareEngineResources(files); });
        auto journal = std::make_shared<SessionJournal>();
        CHECK_THROWS_WITH(NativeProjectSession(1, resources.Files, 1, nullptr, journal), "Native project CreateEngine returned null");
        CHECK(journal->ThreadsStarted == 1);
        CHECK(journal->ThreadsJoined == 1);
        CHECK(journal->EnginesDestroyed == 0);
        CHECK(journal->Count(ProjectJournalEvent::AppCreated) == 0);
        CHECK(GetModuleHandleW(L"NativeProjectV1.dll") == nullptr);
    }, 25000, 1536);
}

TEST_CASE("DLLFIX03 startup exit before readiness reports error and joins destroyed engine", "[native][engine-integration][project-dll][coverage][coverage-remaining][fixture][isolated]")
{
    Isolated([]
    {
        BehaviourFixture resources([](TemporaryDirectory& files) { PrepareEngineResources(files); });
        auto journal = std::make_shared<SessionJournal>();
        CHECK_THROWS_WITH(NativeProjectSession(1, resources.Files, 2, nullptr, journal), "Native project exited before readiness");
        CHECK(journal->ThreadsStarted == 1);
        CHECK(journal->ThreadsJoined == 1);
        CHECK(journal->EnginesDestroyed == 1);
        CHECK(journal->Count(ProjectJournalEvent::Ready) == 0);
        CHECK(journal->Count(ProjectJournalEvent::AppCreated) == 1);
        CHECK(journal->Count(ProjectJournalEvent::AppShutdown) == 1);
        // Worker cleanup is independently observable even when production keeps
        // native world/App alive and fixture must pin this DLL for safety.
        CAPTURE(journal->Count(ProjectJournalEvent::AppDestroyed), GetModuleHandleW(L"NativeProjectV1.dll"));
        CHECK(journal->Count(ProjectJournalEvent::AppDestroyed) == 1);
    }, 25000, 1536);
}

TEST_CASE("DLL-LIFETIME01 typed DLL asset behaviour App and callback release before unload", "[native][engine-integration][project-dll][coverage][coverage-remaining][lifecycle][isolated]")
{
    Isolated([]
    {
        BehaviourFixture resources([](TemporaryDirectory& files) { PrepareEngineResources(files); });
        NativeProjectSession session(1, resources.Files);
        REQUIRE(session.Read() == 1);
        const bool unloaded = session.Close();
        CAPTURE(unloaded, session.ModulePinned, session.WorldStillAlive, session.TeardownSnapshot);
        CHECK(session.Journal->ThreadsStarted == 1);
        CHECK(session.Journal->ThreadsJoined == 1);
        CHECK(session.Journal->EnginesDestroyed == 1);
        // Host owns this snapshot; Close fills it after DestroyEngine and before
        // attempting FreeLibrary. DLL getters/caches cannot manufacture counters.
        const auto count = [&](const ProjectJournalEvent event) { return session.TeardownSnapshot[static_cast<size_t>(event)]; };
        CHECK(count(ProjectJournalEvent::AssetCreated) == 1);
        CHECK(count(ProjectJournalEvent::AssetDestroyed) == 1);
        CHECK(count(ProjectJournalEvent::BehaviourCreated) == 1);
        CHECK(count(ProjectJournalEvent::BehaviourDisposed) == 1);
        CHECK(count(ProjectJournalEvent::AppCreated) == 1);
        CHECK(count(ProjectJournalEvent::AppDestroyed) == 1);
        CHECK(count(ProjectJournalEvent::CallbackCreated) == 1);
        CHECK(count(ProjectJournalEvent::CallbackDestroyed) == 1);
        CHECK(count(ProjectJournalEvent::AppShutdown) == 1);
        CHECK(count(ProjectJournalEvent::Ready) == 1);
        CHECK_FALSE(session.WorldStillAlive);
        CHECK_FALSE(session.ModulePinned);
        CHECK(unloaded);
        CHECK(GetModuleHandleW(L"NativeProjectV1.dll") == nullptr);
    }, 25000, 1536);
}

TEST_CASE("DLLFIX04 timeout before engine publication cancels late creation and joins destroy", "[native][engine-integration][project-dll][coverage][coverage-remaining][fixture][isolated]")
{
    Isolated([]
    {
        BehaviourFixture resources([](TemporaryDirectory& files) { PrepareEngineResources(files); });
        auto journal = std::make_shared<SessionJournal>();
        // DLL blocks inside CreateEngine until host timeout requests cancellation.
        // Constructor first waits for that barrier, so thread scheduling cannot
        // accidentally turn this into a pre-creation cancellation test.
        CHECK_THROWS_WITH(NativeProjectSession(1, resources.Files, 3, nullptr, journal, 10), "Native project creation timeout");
        CHECK(journal->CancelRequested);
        CHECK(journal->Count(ProjectJournalEvent::CreationEntered) == 1);
        CHECK(journal->Count(ProjectJournalEvent::StartEntered) == 0);
        CHECK(journal->Count(ProjectJournalEvent::AppCreated) == 1);
        CHECK(journal->Count(ProjectJournalEvent::AppDestroyed) == 1);
        CHECK(journal->Count(ProjectJournalEvent::Ready) == 0);
        CHECK(journal->ThreadsStarted == 1);
        CHECK(journal->ThreadsJoined == 1);
        CHECK(journal->EnginesDestroyed == 1);
    }, 25000, 1536);
}

TEST_CASE("DLLFIX05 cancellation before actual Start survives its later run-flag store", "[native][engine-integration][project-dll][coverage][coverage-remaining][fixture][isolated]")
{
    Isolated([]
    {
        BehaviourFixture resources([](TemporaryDirectory& files) { PrepareEngineResources(files); });
        auto journal = std::make_shared<SessionJournal>();
        CHECK_THROWS_WITH(NativeProjectSession(1, resources.Files, 5, nullptr, journal, 10000, 10), "Native project readiness timeout");
        CHECK(journal->CancelRequested);
        CHECK(journal->Count(ProjectJournalEvent::StartEntered) == 1);
        CHECK(journal->Count(ProjectJournalEvent::Ready) == 0);
        // Cancellation hook invokes actual Engine::Shutdown after Start sets its
        // run flag. No fixture overwrites that flag or substitutes shutdown.
        CHECK(journal->Count(ProjectJournalEvent::AppShutdown) == 1);
        CHECK(journal->ThreadsStarted == 1);
        CHECK(journal->ThreadsJoined == 1);
        CHECK(journal->EnginesDestroyed == 1);
    }, 25000, 1536);
}

TEST_CASE("DLLFIX06 explicit Close reports copied worker exception after readiness", "[native][engine-integration][project-dll][coverage][coverage-remaining][fixture][isolated]")
{
    Isolated([]
    {
        BehaviourFixture resources([](TemporaryDirectory& files) { PrepareEngineResources(files); });
        NativeProjectSession session(1, resources.Files, 4);
        REQUIRE(session.Read() == 1);
        try
        {
            session.Close();
            FAIL("Worker exception was lost by explicit Close");
        }
        catch (const std::runtime_error& error)
        {
            CHECK(typeid(error) == typeid(std::runtime_error));
            CHECK(std::string(error.what()) == "Injected native worker failure after readiness");
        }
        CHECK(session.Journal->WorkerErrorsCopied == 1);
        CHECK(session.Journal->Count(ProjectJournalEvent::WorkerExceptionDestroyed) == 1);
        CHECK(session.Journal->ThreadsJoined == 1);
        CHECK(session.Journal->EnginesDestroyed == 1);
        CHECK(session.Journal->Count(ProjectJournalEvent::Ready) == 1);
        CHECK(session.Journal->Count(ProjectJournalEvent::AppShutdown) == 1);
        CHECK(session.TeardownSnapshot[static_cast<size_t>(ProjectJournalEvent::WorkerExceptionDestroyed)] == 1);
    }, 25000, 1536);
}
