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

Categories are `Unit`, `FileSystem`, `Headless`, and `Composition`. `Area` names identify features. The default run includes every category and all regression tests, including known failures. A focused run does not replace the final full run.

## Reports

```powershell
dotnet test ReiEditor.Tests/ReiEditor.Tests.csproj --no-build -c Debug -p:Platform=x64 --settings ReiEditor.Tests/coverage.runsettings --logger trx --collect:"XPlat Code Coverage" --results-directory ReiEditor.Tests/TestResults
```

TRX and `coverage.cobertura.xml` appear under `ReiEditor.Tests/TestResults`; the existing gitignore excludes this directory. Coverage includes the entire ReiEditor assembly, including currently untested UI/interop code, and excludes test/MCP/third-party assemblies. There is no percentage gate in this slice. Low initial coverage is expected; examples do not complete the feature backlog. Any later coverage exclusions must be documented.

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

## Initial examples

| Area | Contract demonstrated |
|---|---|
| Hierarchy | Reject descendant cycle without mutation/events |
| Selection | Publish selected entity and avoid duplicate engine selection calls |
| Resources | Create nested directories and read written JSON |
| Property editors | UI-thread notification and unsubscribe on Dispose |
| Startup | Real SerializationModule binary wire format/lifetime; Factory typed parameters |
| Preferences | InitializeAsync waits for controlled storage read |
| Test infrastructure | Unique directory cleanup and path escape rejection |

The four representative scenarios are supplemented by composition, async initialization, and temporary-directory safeguards. They are starter coverage, not complete suites for these areas.

Test replacements live in `Infrastructure/TestDoubles` and use the `Test` prefix consistently. Their names identify the replaced dependency; each implementation makes its recording or response behavior explicit.

Tests, infrastructure, and this README form the first slice. Planning and scenario research documents stay local and are excluded from commits. Bug fixes and wider feature coverage belong to subsequent slices; commits require an explicit user request.

## Slice workflow

The Core slice covers observable state, commands, pools, navigation, path/file utilities, JSON and binary serialization, serialized properties/components, type and shader parsing, render data/math, and asset migrations. Coverage measures the whole editor; these suites do not imply complete coverage of every contract in those areas.

The scene slice adds hierarchy/entity models, selection and commands, state read/write and reconciliation, scene loading, default templates, asset drop, and canvas/layout service tests. Engine selection and snapshot reconciliation use the headless UI dispatcher. Native execution, OS drag sessions, and timer-driven polling remain outside these suites; explicit synchronization calls do not require sleeps.

The asset slice covers registry/metadata, resource IO and search, file operations and commands, asset load/save/import, creation utilities, supported C++ reflection parsing, scripting registries/code generation/component refresh, and shader/runtime dispatch. File operations use isolated project and engine-source fixtures. Generated C++ is checked as text; no native compiler or engine is invoked.

The projects/settings slice covers active projects and bookmarks, creation validation, template and solution generation, project creation/update/setup/deletion, engine resource copying, editor preferences, configuration validation and engine-settings reloads. Storage is controlled in memory; project files and deletion targets use owned temporary directories. Positive MSBuild version validation remains deferred until a controlled versioned executable fixture or version-reader seam exists.

The build slice covers execution paths and MSBuild arguments, source/build snapshots, preparation and command guards, stage selection, cache manifests and hashing, asset packing/maps, output staging/promotion, standalone packaging and engine-stop gating. Build and engine interfaces supply controlled results or write known fixture bytes. The real process builder is tested only at invalid-path guards; native sessions and external tools are never started. Timing assertions use controlled task gates; production polling/settling delays remain unchanged.

The editor workflow slice covers console/logging, refresh and disk restore, playmode guards and stop/save/build/start orchestration, engine startup/failure/shutdown, viewport controls and hotkeys, managed input dispatch, and PNG capture from owned RGBA buffers. Engine callbacks use managed delegates through a controlled `IEngineApi`; no engine DLL or GPU is loaded. Capture encoding uses the existing SkiaSharp dependency. Fire-and-forget negative paths without observable completion, watcher integration, precise timer races, and the capture byte-count limit remain deferred. Test classes and test methods include XML summaries.

The ViewModel slice covers search and asset pickers, hierarchy and project browser selection/navigation/operations, property and reference editors, RectTransform editing, component drawers, basic material/monitor state, project and asset creation forms, settings validation, build progress/cancellation, console details/filtering, status/interaction overlay, playmode options/panel, tabs, save commands and dialog ownership. Headless tests assert state, events and command effects; no XAML or appearance snapshots are used. Build tasks use controlled gates with terminal cleanup. Native window behavior, platform pickers, non-empty-output confirmation, inaccessible monitor/texture workers and exact debounce/poll races remain deferred.

The full run contains active regressions for `1f` parsing as `10`, generated-file/meta extension casing, and `OtherProject/Scripts/bin` filtering. Scene regressions additionally check duplicate hierarchy insertion atomicity, cross-parent move source index, oversized same-parent insertion, and preservation of a surviving child when its old parent disappears from an engine snapshot. Production fixes are separate work. These failures remain in the default run and are not skipped or filtered out.

Asset regressions cover repeated filename segments in resource writes, missing-extension validation during creation, duplicate scene entities after reload, deleted enum/property definitions surviving refresh, and code lost between separate block comments.

Project/settings regressions cover uppercase source extensions and XML include escaping, default-scene fallback with an empty build configuration, loading-procedure cleanup after setup failures, and preserving prior engine settings when a replacement has no valid version.

Build regressions cover success published after cancellation during asset building, compound `.vcxproj.user` paths entering asset packing, and case-sensitive exclusions for source/project/meta files in the packing filter.

ViewModel regressions cover stale enum selection after external edits, original vector-child subscriptions surviving replacement/disposal, custom child editors recreated on unchanged data, descendant hierarchy VMs surviving controller disposal, sibling project paths passing move validation, console/status/playmode subscriptions surviving disposal, and a created dialog VM leaking when its window factory throws. These tests remain active; production fixes are separate work.

1. Select one slice and plan its scenarios, alone or with research agents.
2. Implement tests and the necessary infrastructure. Every test class and every test method must have an XML `/// <summary>` describing its scope or expected behavior.
3. Run the relevant builds and tests from the main task.
4. Before each commit, create a new GPT-6 Astra agent with High reasoning for an independent review of the proposed changes: correctness, test quality, refactoring, cleanup, and regressions. Include untracked files in the review. Resolve findings and revalidate changes before reporting to the user.
5. Present the result and remaining issues to the user. Commit only after explicit approval, excluding local plans and research.
6. Report what was completed and what remains, then proceed to the next slice.

A review applies to the changes actually reviewed. If further changes are made before the commit, include them in the review; do not treat an earlier review as approval of unseen changes.
