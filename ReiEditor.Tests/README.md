# ReiEditor.Tests

Infrastructure and feature tests for the .NET 10/x64 editor. No native engine DLL, GPU, production application startup, or visible window is required. Run commands from the repository root.

## Run

```powershell
dotnet build ReiEditor/ReiEditor.csproj -c Debug -p:Platform=x64
dotnet test ReiEditor.Tests/ReiEditor.Tests.csproj -c Debug -p:Platform=x64
dotnet test ReiEditor.Mcp.Tests/ReiEditor.Mcp.Tests.csproj -c Debug
```

Use the specific C# project, not `dotnet test Rei.sln`: the solution also contains native projects. Solution Debug/Release configurations map this project to x64; solution Tests maps it to Release/x64, matching ReiEditor.

## Focused runs

After building the same configuration/platform:

```powershell
dotnet test ReiEditor.Tests/ReiEditor.Tests.csproj --no-build -c Debug -p:Platform=x64 --filter "Category=Unit"
dotnet test ReiEditor.Tests/ReiEditor.Tests.csproj --no-build -c Debug -p:Platform=x64 --filter "Category=Headless"
dotnet test ReiEditor.Tests/ReiEditor.Tests.csproj --no-build -c Debug -p:Platform=x64 --filter "Area=Resources"
```

Categories are `Unit`, `FileSystem`, `Headless`, and `Composition`. `Area` names identify features. The default run includes every category and all regression tests, including known failures. Choose focused suites for the changed behavior; broaden runs for changes crossing several areas.

## Reports

```powershell
dotnet test ReiEditor.Tests/ReiEditor.Tests.csproj --no-build -c Debug -p:Platform=x64 --settings ReiEditor.Tests/coverage.runsettings --logger trx --collect:"XPlat Code Coverage" --results-directory ReiEditor.Tests/TestResults
```

TRX and `coverage.cobertura.xml` appear under `ReiEditor.Tests/TestResults`; the existing gitignore excludes this directory. Coverage includes the entire ReiEditor assembly, including currently untested UI/interop code, and excludes test/MCP/third-party assemblies. There is no percentage gate. Any coverage exclusions must be documented.

Package versions match the existing MCP test stack: xUnit 2.9.3, runner 3.1.4, Test SDK 17.14.1. Headless is pinned to Avalonia 11.0.0. Reports use [coverlet.collector 6.0.4](https://www.nuget.org/packages/coverlet.collector/6.0.4).

## Test isolation

- Construct the subject directly. Use the real production Autofac module only for composition tests; do not duplicate its registrations or start ApplicationScope/EditorScope.
- `TemporaryDirectory` owns one unique directory under the OS temp directory. `GetPath` rejects traversal and foreign absolute paths; disposal deletes only its owned root. `TemporaryProjectFixture` supplies an active in-memory Project and real ResourceService. No current-directory changes or user preference writes. Do not create junctions/symlinks in these fixtures.
- `TestEntityApi` records copied selection IDs and supports explicitly configured scene/entity snapshot reads; all unconfigured operations fail. `TestEditorStorageService` controls asynchronous reads and rejects writes. Extend doubles only when a test needs a new operation; do not silently return success for unexpected calls.
- `TestLogger<T>` captures messages/exceptions, with snapshots safe for background logging. Builders create only the domain shapes currently needed.
- `TestData` contains small source-controlled fixtures copied to test output. File tests copy/write them to their own temporary project, never mutate shared output fixtures.
- Use TaskCompletionSource with RunContinuationsAsynchronously to gate async work. Await operations and use bounded waits; do not synchronize with sleeps. Release gates in finally, dispose the subject, remove subscriptions, then delete files. Initialize IAsyncInitializable before using its loaded state.
- `[AvaloniaFact]` and `[Collection(HeadlessCollection.NAME)]` identify UI-thread tests. `AvaloniaTestApplication` points to an empty Application with the headless backend; it never calls ReiEditor.App or Program. The collection prevents overlap with other test collections. Ordinary facts do not start Avalonia. Add themes/resources only when a later test needs controls.
- The property VM example explicitly dispatches its update to UI; it does not claim that IntegerPropertyViewModel marshals arbitrary background changes itself.
- `InternalsVisibleTo("ReiEditor.Tests")` grants access to internal capture implementations. Runtime interfaces and behavior remain unchanged.

## Coverage and limits

- Core: serialization, property models, parsing, math, paths and extension matching.
- Scenes: hierarchy, selection, scene loading/failure recovery, runtime snapshots, parenting and clone requests.
- Assets: imports, metadata, schemas, generated C++ serializers, cache invalidation and binary packing.
- Build/lifecycle: incremental build decisions, PCH dependencies, cancellation, Play/Stop and DLL loading guards.
- Editor: console ordering/batching/clearing, procedure status, interaction overlay, commands, ViewModels and selected DI graphs.

Engine interactions use controlled doubles. Generated C++ is checked as text; these tests do not compile it or prove native synchronization. Headless tests check state/events, not rendered XAML, platform windows or OS input. Use [real-engine tests](../Rei.EngineIntegration.Tests/README.md) for native state, scene transitions, dependency lifetimes, Play/Stop, DLL reload and persistence.

## Adding tests

Reuse the fixtures above. Cover observable behavior, failures and lifecycle boundaries. Every test class and method should have an XML summary explaining its scope. Use controlled async gates and bounded waits. Build/test selection, documentation, reviews and commits follow the repository AGENTS.md.
