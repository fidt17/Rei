# Rei.EngineIntegration.Tests

Windows/x64 tests using a real ReiEditor, native Rei.dll and isolated project DLLs. Editor values and native runtime values are read independently through MCP; persistence is checked separately on disk. Fast harness/fixture tests run without an Editor.

## Build and run

Requires a Windows graphics session, .NET 10 SDK and MSBuild 18 with the C++ toolchain. Build Rei and ReiSandbox sequentially; build Editor independently. Run from the repository root:

```powershell
$repo = 'C:\Repos\Rei'
$msbuild = 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\amd64\MSBuild.exe'
& $msbuild "$repo\Rei.sln" /t:Rei /p:Configuration=Debug /p:Platform=x64 /m:1
& $msbuild "$repo\Rei.sln" /t:ReiSandbox /p:Configuration=Debug /p:Platform=x64 /m:1
dotnet build "$repo\ReiEditor\ReiEditor.csproj" -p:Platform=x64
$tests = "$repo\Rei.EngineIntegration.Tests\Rei.EngineIntegration.Tests.csproj"
dotnet build $tests

$env:REI_RUN_ENGINE_TESTS = '1'
$env:REI_TEST_EDITOR_EXE = "$repo\ReiEditor\bin\x64\Debug\net10.0\ReiEditor.exe"
$env:REI_TEST_ENGINE_FILE = "$repo\ReiEngine.rei_engine"
$env:REI_TEST_MSBUILD = $msbuild
dotnet test $tests --no-build --no-restore --filter 'Suite=Smoke'
```

Without `REI_RUN_ENGINE_TESTS=1`, native cases are explicitly skipped. Once enabled, missing prerequisites, startup failures and timeouts fail tests. `--no-build` skips the managed runner build; fixture native builds still run. Do not rebuild shared engine binaries during a run.

## Test selection

| Filter | Purpose |
| --- | --- |
| `Suite=Smoke` | Asset/material synchronization, independent runtime reads and Play/Stop rollback. |
| `Suite=Lifecycle` | Persistence, scene switching, imports, build invalidation, DLL reload, logging and shutdown. |
| `Area=SceneSwitching` | Editor/native scene changes, fresh dependencies, failure recovery and startup configuration. |
| `Area=Profiling` | Native CPU profiler contracts; also includes opt-in benchmarks. |
| `Category=EngineIntegration` | All real-engine cases; external benchmarks require their own opt-in. |

Use `FullyQualifiedName~ClassOrMethod` for a focused regression. Fixture integrity, copy validation and harness unit tests need no engine opt-in. Choose suites for changed behavior; use Smoke and relevant Lifecycle cases for Editor/native synchronization.

Smoke cases share one lazily started Editor and initial project build. Each case restores Editor/native baselines and checks asset file hashes. They must not save, alter source files, create/delete persistent assets or rebuild DLLs. A failed reset blocks subsequent smoke cases.

Lifecycle cases own fresh harnesses. `RestartAsync` keeps their project/storage/output directory; it does not copy the fixture over saved files. Restart terminates the owned process without implicit saving. Process termination and interactive Editor shutdown have separate coverage. Ordinary fixture runs do not reuse native build outputs across test invocations.

## Coverage boundaries

Regression scenarios cover loaded/unloaded assets, dependency replacement, invalid values, material uniforms, scene transitions without engine restart, failed scene recovery, Editor `LastSceneId` versus game scene 0, native Play-mode transitions, selection, hierarchy snapshots, PCH/header invalidation, ordered logs, Play/Stop, DLL reload, shutdown and saved/unsaved values across restart.

MCP setters exercise application services. Successful requests and Editor caches are not proof of native state: read runtime independently and compare serialized values with explicit float tolerances. Headless Monitor debounce/UI tests belong to ReiEditor.Tests. OS input, rendered XAML and every possible reference-collection/behaviour combination are not covered here.

CPU profiling lifecycle tests validate exact project-DLL metrics, captures, session changes, limits and concurrent reads. Their deliberate scope waits test profiler contracts, not performance.

## External-project CPU benchmarks

External projects require `REI_PROFILE_PROJECT` in addition to the engine opt-in. Supply a directory containing exactly one `.rei`, not the file itself:

```powershell
$env:REI_PROFILE_PROJECT = 'D:\Projects\TheShed\ReiProject'
dotnet test $tests --no-build --no-restore --filter 'FullyQualifiedName~CpuProfilingBenchmarkTests'
Remove-Item Env:REI_PROFILE_PROJECT
```

Only the owned copy is opened, built, imported or saved. Copied project/source paths are localized; stale MSBuild tracking files are removed. Links, overlapping roots and escaping/unverifiable build paths are rejected. Use trusted project build commands: process isolation is not an MSBuild sandbox. Source fingerprints before/after verify that the original, including caches/binaries, stayed unchanged.

`PreparedBenchmarkProject` can reuse a validated owned copy/cache when source input fingerprints match. It restores the source startup scene before reuse. Ordinary fixture tests remain fresh. External startup defaults to 15 minutes; `REI_PROFILE_STARTUP_TIMEOUT_SECONDS` accepts positive values up to 1800. Fixture startup uses four minutes.

The CPU benchmark checks engine freshness, clean-builds the owned project, restarts, verifies engine/project DLLs, warms 120 frames and records three 120-frame native captures. Results and input/binary hashes stay in the run directory. Paired/resumed CPU experiments have separate opt-in attributes in `PairedCpuProfilingBenchmarkTests`.

Compare the same scene, camera, mode, viewport and build settings without competing builds or captures. Current non-image MCP APIs do not expose camera matrices/native viewport dimensions; fresh-layout results do not prove equivalence with an older user-layout baseline. CPU GL-call time includes driver waits, not GPU execution time.

Temporary Hidden Alley load-stage timers, JSONL timing export and startup A/B tests have been removed. Normal frame profiling remains available through MCP; see [bridge contracts](../docs/mcp-bridge.md).

## Ownership and diagnostics

Runs live under `%USERPROFILE%\Rei-engine-tests\<32-digit GUID>`. The location avoids MSBuild's AppData/Temp dependency exclusions. Each harness owns its copied `project`, isolated `storage` preferences, loopback port, Editor process and children. Readiness verifies the exact project path before mutations. Existing user Editors/projects are untouched.

Diagnostics include `stdout-N.log`, `stderr-N.log`, `mcp.jsonl`, `timings.jsonl`, captures and fixture sources. Harness timings describe startup/operations, independently of native frame profiling. Output reports the run path. Bounded waits and reliable teardown belong in the shared `EngineIntegrationHarness`; reuse it instead of one-off launch scripts.

`DisposeAsync` stops owned processes, then removes only that run's `project/bin` by default. Logs, preferences, fixture sources and captures remain. `StopAsync`/restart do not trim mid-session. To retain binaries/symbols, pass `keepBuildOutputs: true` or set `REI_TEST_KEEP_BUILD_OUTPUTS=true` before constructing the harness. Prepared benchmarks retain outputs for reuse.

Cleanup validates the canonical root, immediate GUID child and absence of links. Locked-file failures leave `cleanup-error.txt`. Interrupted runners can leave outputs; there is no age/count retention policy. Before manual cleanup, stop that run's owners, preserve needed evidence and verify the exact resolved path lies beneath this root with no links. Delete only reviewed completed GUID directories; never the root, source checkout or unrelated processes. Removing retained `project/bin/int` discards incremental build state and requires regeneration.
