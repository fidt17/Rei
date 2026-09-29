# Real engine integration tests

Starts a separate ReiEditor with the real native engine and a project DLL compiled from the checked-in DataAssets fixture. No Symbols checkout or user project is required. Mocked Editor/MCP tests do not provide this coverage.

## Run

Requires Windows with a graphics session, .NET 10 SDK, MSBuild 18/C++ toolchain. Build native projects sequentially through Rei.sln so outputs match ReiEngine.rei_engine.

```powershell
$repo = "C:\Repos\Rei"
$msbuild = "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\amd64\MSBuild.exe"
& $msbuild "$repo\Rei.sln" /t:Rei /p:Configuration=Debug /p:Platform=x64 /m:1
& $msbuild "$repo\Rei.sln" /t:ReiSandbox /p:Configuration=Debug /p:Platform=x64 /m:1
dotnet build "$repo\ReiEditor\ReiEditor.csproj" -p:Platform=x64 -p:OutDir="$repo\.tmp\integration-editor\"
$env:REI_RUN_ENGINE_TESTS = "1"
$env:REI_TEST_EDITOR_EXE = "$repo\.tmp\integration-editor\ReiEditor.exe"
$env:REI_TEST_ENGINE_FILE = "$repo\ReiEngine.rei_engine"
$env:REI_TEST_MSBUILD = $msbuild
dotnet test "$repo\Rei.EngineIntegration.Tests\Rei.EngineIntegration.Tests.csproj"
```

Without REI_RUN_ENGINE_TESTS=1, real-engine tests are explicitly skipped. Once enabled, missing prerequisites, startup/build failures, and timeouts fail tests.

## Isolation and diagnostics

Each harness owns a unique directory under %TEMP%/Rei-engine-tests, a copied fixture, build outputs, separate preferences (REI_EDITOR_STORAGE), and an explicit startup project (REI_STARTUP_PROJECT). It chooses an unused loopback port. Port acquisition has a small bind race; readiness verifies the exact project path before mutations.

Teardown terminates only the owned Editor process and children. Existing user Editors are untouched. Run directories remain for diagnostics: stdout.log, stderr.log, mcp.jsonl, project files and native build outputs. Remove old directories manually when no longer needed. Do not rebuild shared engine binaries while integration tests run.

## Coverage

- Project/Monitor selection through normal selection services; invalid asset/source requests.
- Independent Editor and native values after edits.
- Unloaded asset inspection without implicit native loading.
- Typed dependency replacement and native dependency loading.
- Large JSON, Unicode and collection values.
- Play-session rollback versus Editor save and disk state.
- Project DLL rebuild/reload followed by further synchronization.

EngineIntegrationHarness provides bounded MCP calls, operation waits, condition waits, independent reads, fixture ownership, logs and teardown. Reuse it for new scenarios. MCP setters exercise the service path; Monitor debounce tests belong to the Editor test suite, not simulated UI input here.
