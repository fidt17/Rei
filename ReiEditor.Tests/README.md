# ReiEditor.Tests

Initial infrastructure for .NET 10/x64 editor tests. No native DLL, GPU, production application startup, or visible window is required. Run commands from the repository root.

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
- `TestEntityApi` records copied selection IDs and rejects all other API calls. `TestEditorStorageService` controls asynchronous reads and rejects writes. Extend doubles only when a test needs a new operation; do not silently return success for unexpected calls.
- `TestLogger<T>` captures messages/exceptions, with snapshots safe for background logging. Builders create only the domain shapes currently needed.
- `TestData` contains small source-controlled fixtures copied to test output. File tests copy/write them to their own temporary project, never mutate shared output fixtures.
- Use TaskCompletionSource with RunContinuationsAsynchronously to gate async work. Await operations and use bounded waits; do not synchronize with sleeps. Release gates in finally, dispose the subject, remove subscriptions, then delete files. Initialize IAsyncInitializable before using its loaded state.
- `[AvaloniaFact]` and `[Collection(HeadlessCollection.NAME)]` identify UI-thread tests. `AvaloniaTestApplication` points to an empty Application with the headless backend; it never calls ReiEditor.App or Program. The collection prevents overlap with other test collections. Ordinary facts do not start Avalonia. Add themes/resources only when a later test needs controls.
- The property VM example explicitly dispatches its update to UI; it does not claim that IntegerPropertyViewModel marshals arbitrary background changes itself.
- No InternalsVisibleTo or production interface changes are needed yet. Add friend-assembly access only when testing internal implementations.

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

1. Select one slice and plan its scenarios, alone or with research agents.
2. Implement tests and the necessary infrastructure. Every test class and every test method must have an XML `/// <summary>` describing its scope or expected behavior.
3. Run the relevant builds and tests from the main task.
4. Before each commit, create a new GPT-6 Astra agent with High reasoning for an independent review of the proposed changes: correctness, test quality, refactoring, cleanup, and regressions. Include untracked files in the review. Resolve findings and revalidate changes before reporting to the user.
5. Present the result and remaining issues to the user. Commit only after explicit approval, excluding local plans and research.
6. Report what was completed and what remains, then proceed to the next slice.

A review applies to the changes actually reviewed. If further changes are made before the commit, include them in the review; do not treat an earlier review as approval of unseen changes.
