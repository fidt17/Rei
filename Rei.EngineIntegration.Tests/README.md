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
dotnet test "$repo\Rei.EngineIntegration.Tests\Rei.EngineIntegration.Tests.csproj" --filter "Suite=Smoke"
```

Without REI_RUN_ENGINE_TESTS=1, real-engine tests are explicitly skipped. Once enabled, missing prerequisites, startup/build failures, and timeouts fail tests.

## Selective runs and process lifetime

All real-engine cases have Category=EngineIntegration, Suite=Smoke or Suite=Lifecycle, and an Area trait.
REI_RUN_ENGINE_TESTS=1 remains the explicit opt-in. Filtering alone does not enable native tests.
Fixture integrity and harness unit tests run without the opt-in and never launch Editor.

After building the test project once, use --no-build --no-restore to avoid rebuilding the managed test runner.
This does not skip the isolated project's first native build.

    $tests = "$repo\Rei.EngineIntegration.Tests\Rei.EngineIntegration.Tests.csproj"
    dotnet test $tests --no-build --no-restore --filter "Suite=Smoke"
    dotnet test $tests --no-build --no-restore --filter "Suite=Smoke&Area=Materials"
    dotnet test $tests --no-build --no-restore --filter "FullyQualifiedName~DependencyReplacementLoadsNativeAsset"
    dotnet test $tests --no-build --no-restore --filter "Suite=Lifecycle"
    dotnet test $tests --no-build --no-restore --filter "Area=Profiling"
    dotnet test $tests --no-build --no-restore --filter "Category=EngineIntegration"

Smoke cases share one lazily started Editor and one initial native project build per test run.
They execute sequentially in an xUnit collection. Setup loads the observed assets and saves their normalized
Editor representation once before capturing the baseline. Each case enters PlayMode and stops it in finally.
Before and after every case, the fixture checks baseline Editor values, independent native values/load status,
and hashes of all project .asset/.mat files. Failed reset prevents later cases from using contaminated state.
Smoke cases must not save, create/delete persistent assets, change source files, or rebuild the DLL.
Keep those operations in isolated lifecycle cases. Adding a new mutable asset to smoke requires including
its Editor/native state in the reset baseline.

Lifecycle cases each own a fresh harness. DLL rebuild, project creation, and process restart are independently
selectable. The restart case launches twice against the same owned project/storage/build outputs, without
copying the fixture over saved files. Restart terminates the owned process without an implicit save; it tests
persistence across process termination, not the interactive close/save dialog.

A new dotnet test invocation starts a new Editor. No reuse across invocations or attachment to a user Editor.
A filtered lifecycle-only run does not start an unused smoke Editor. No cross-run native build cache yet:
each fresh project proves that fixture sources compile, avoiding stale generated code or DLLs.

Use smoke for asset/MCP synchronization changes; lifecycle for persistence, imports, build/DLL loading,
Play/Stop, or harness changes. Run both for release validation. Pure parsing/settings/conversion changes
can normally use focused Editor unit tests first; these are selection guidelines, not automatic dependency detection.

## Isolation and diagnostics

Each harness owns a unique directory under %TEMP%/Rei-engine-tests, a copied fixture, build outputs, separate preferences (REI_EDITOR_STORAGE), and an explicit startup project (REI_STARTUP_PROJECT). It chooses an unused loopback port. Port acquisition has a small bind race; readiness verifies the exact project path before mutations.

Teardown terminates only the owned Editor process and children. Existing user Editors are untouched. Run directories remain for diagnostics: stdout-N.log and stderr-N.log per process launch, mcp.jsonl, timings.jsonl, project files and native build outputs. Test output includes artifact paths and shared Editor PID. timings.jsonl records startup, operations, and smoke-case durations with process/launch identifiers. Remove old directories manually when no longer needed. Do not rebuild shared engine binaries while integration tests run.

## Temporary artifacts and cleanup

`EngineIntegrationHarness` creates a new `%TEMP%\Rei-engine-tests\<32-digit GUID>` directory per harness. It copies a fixture into `project`, builds the native project there, and stores isolated Editor preferences in `storage`. Smoke cases share one harness; lifecycle cases and separate test/helper invocations create additional directories. `RestartAsync` reuses the current harness directory. `DisposeAsync` stops owned processes but does **not** delete files. There is no automatic retention limit, cleanup on success, or cleanup on failure.

Most disk space comes from native build intermediates (`project\bin\int`: `.obj`, `.idb`, `.tlog`), incremental linker files (`.ilk`), symbols (`.pdb`), copied engine/project DLLs, imported resource caches and standalone packages. A Symbols run built in both EditorDebug and Debug can exceed 1 GiB. Logs and framebuffer PNGs are much smaller. Enabling `--no-build --no-restore` only skips the managed runner build; it does not reuse a previous harness's native project or remove its outputs.

### Before cleanup

1. Finish/stop test runners and diagnostic helpers, including their isolated Editors, standalone processes and native builds. Do not start another integration run during cleanup. Age alone does not prove that a directory is inactive. Do not kill unrelated user Editors or MSBuild processes to make cleanup possible.
2. Copy needed evidence to a durable location outside this temp root: `stdout-*.log`, `stderr-*.log`, `mcp.jsonl`, `timings.jsonl`, profiling JSON and `frame-*.png`. Preserve the changed fixture/source assets and symbols too when needed to reproduce a failure or debug a crash. Symbols performance reports belong in `C:\Repos\Symbols\output\profiling`.
3. Preview the exact directories. Delete only immediate GUID-named children of this specific root. Never delete `%TEMP%` itself, a source checkout, or directories containing junctions/symlinks.

### Inspect sizes and last activity

Run in PowerShell. This example uses the current Windows user's temp path; on Vladimir's machine it is `C:\Users\Vladimir Korzh\AppData\Local\Temp\Rei-engine-tests`. The scan is read-only. Read errors stop it; links are rejected. `LastActivityUtc` includes creation time and descendant timestamps because the top directory's `LastWriteTime` does not reflect every nested write.

```powershell
$testRunsRoot = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) 'Rei-engine-tests')).TrimEnd('\')
$rootInfo = Get-Item -LiteralPath $testRunsRoot -Force -ErrorAction Stop
if (!$rootInfo.PSIsContainer -or ($rootInfo.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
    throw 'Expected an ordinary Rei-engine-tests directory.'
}

$runs = @(Get-ChildItem -LiteralPath $testRunsRoot -Directory -Force -ErrorAction Stop |
    Where-Object { $_.Name -match '^[0-9a-fA-F]{32}$' } |
    ForEach-Object {
        $run = $_
        if ($run.Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw "Refusing linked run directory: $($run.FullName)"
        }
        $entries = @(Get-ChildItem -LiteralPath $run.FullName -Recurse -Force -ErrorAction Stop)
        if ($entries | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) {
            throw "Refusing run containing links: $($run.FullName)"
        }
        $bytes = ($entries | Where-Object { !$_.PSIsContainer } | Measure-Object Length -Sum).Sum
        $timestamps = @($run.CreationTimeUtc, $run.LastWriteTimeUtc) + @($entries | ForEach-Object { $_.LastWriteTimeUtc })
        [PSCustomObject]@{
            Path = $run.FullName
            GiB = [Math]::Round($bytes / 1GB, 3)
            LastActivityUtc = ($timestamps | Sort-Object -Descending | Select-Object -First 1)
        }
    })
$runs | Sort-Object GiB -Descending | Format-Table -AutoSize
```

### Remove selected completed runs

After meeting the preconditions above, select old runs (seven days is an example retention period, not an automatic policy). For one known completed run, replace `$selectedPaths` with its exact `Path` copied from the inventory above. Use the same path spelling throughout; do not mix Windows 8.3 aliases (such as `VLADIM~1`) with expanded directory names. For all completed runs, use `@($runs.Path)` only after reviewing and archiving everything needed. The preview below does not remove files. Review it first; then remove **only** `-WhatIf` from `Remove-Item` and rerun the block to perform deletion.

```powershell
$cutoffUtc = [DateTime]::UtcNow.AddDays(-7)
$selectedPaths = @($runs | Where-Object { $_.LastActivityUtc -lt $cutoffUtc } | Select-Object -ExpandProperty Path)

foreach ($selectedPath in $selectedPaths) {
    $resolvedRun = Get-Item -LiteralPath ([IO.Path]::GetFullPath($selectedPath)) -Force -ErrorAction Stop
    if (!$resolvedRun.PSIsContainer -or
        $resolvedRun.Name -notmatch '^[0-9a-fA-F]{32}$' -or
        $resolvedRun.Parent.FullName.TrimEnd('\') -ine $testRunsRoot -or
        ($resolvedRun.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw "Refusing path outside the intended run directories: $selectedPath"
    }
    $entries = @(Get-ChildItem -LiteralPath $resolvedRun.FullName -Recurse -Force -ErrorAction Stop)
    if ($entries | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) {
        throw "Refusing run containing links: $selectedPath"
    }
    Remove-Item -LiteralPath $resolvedRun.FullName -Recurse -WhatIf -ErrorAction Stop
}
```

If a run is locked, stop its owner or wait for teardown and retry. Do not bypass errors or force-close unrelated processes. Failed or interrupted runs use the same cleanup procedure; retain their diagnostics until the failure has been investigated. Unit-only harness tests may leave small/empty GUID directories; these can also be removed when finished.

To preserve a completed test project for investigation while reducing space, its `project\bin\int` directory can be removed separately after verifying the exact path stays inside that run and contains no links. This discards native incremental-build state; the next build of that copy must regenerate it. Keep `.pdb` files if native debugging is needed. No source repository `bin`, `.meta` IDs or SDK outputs need to be changed to clean this temp root. New test runs recreate their own fixtures and outputs.

## Coverage

- Smoke / DataAssets: Project/Monitor selection through normal selection services; invalid asset/source requests.
- Smoke / DataAssets: bool, signed/unsigned integer, float, enum, vectors, color, large Unicode, and collection grow/clear/shrink.
- Smoke / DataAssets: unloaded asset inspection and editing without implicit native loading.
- Smoke / DataAssets: typed dependency replacement and native dependency loading.
- Smoke / DataAssets: wrong-type/missing references and invalid values rejected without changing Editor/native state.
- Smoke / Materials: scalar/color shader uniform edits independently read from Editor and native material.
- Every smoke case: Play/Stop rollback, disk unchanged, baseline/load-state restoration, one shared process.
- Lifecycle: saved values across DLL rebuild/reload, further edits, Play rollback, disk state.
- Lifecycle: saved values survive full process restart; unsaved changes disappear; sync works after restart.
- Lifecycle: newly created assets remain unloaded in native state during inspection.
- Lifecycle / Time: native frame-clock readings, first-frame delta, elapsed progress, stable same-frame values and three Play/Stop resets; test asset disk bytes unchanged.
- Lifecycle / Profiling: exact native project-DLL scope/counter totals, queued/busy/completed captures, session mismatch, output limits, concurrent reads during Stop, three Play/Stop resets, DLL rebuild/reload and unchanged asset/material/scene bytes. Active test captures deliberately sleep in a child scope; timing is not a performance benchmark.
- Harness: concurrent diagnostic writes preserve every JSONL record. Optional RunStandaloneSmokeAsync uses only an isolated project build/resources, bounded startup/exit, native startup/shutdown logs and owned-process cleanup. Symbols EyeProfilingTests exercises it after native UI draw-counter checks; it is not part of the generic DataAssets fixture.

Not yet covered: unavailable-engine edits, nested reference collections, multiple behaviour consumers observing
the same asset, Monitor UI input/debounce through native readback, entity/behaviour synchronization, and
interactive Editor shutdown. These require additional fixture scenarios or automation capabilities.

EngineIntegrationHarness provides bounded MCP calls, operation waits, condition waits, independent reads, fixture ownership, logs and teardown. Reuse it for new scenarios. MCP setters exercise the service path; Monitor debounce tests belong to the Editor test suite, not simulated UI input here.
