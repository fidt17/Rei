# ReiEditor MCP bridge

Local MCP server embedded in ReiEditor. Tools inspect the current session and use the same services as Editor UI for scene edits, asset operations, build and Play/Stop.

## Connection

ReiEditor must be running. Transport is Streamable HTTP; clients reconnect without owning the Editor process.

| Setting | Value |
| --- | --- |
| MCP endpoint | `http://127.0.0.1:18777/mcp` |
| Health endpoint | `http://127.0.0.1:18777/health` |
| `REI_MCP_ENABLED` | `true` by default; `false` disables server. |
| `REI_MCP_PORT` | `18777` by default; valid range `1..65535`. |

Bind address and paths are fixed. Host headers accept only `127.0.0.1` and `localhost`; CORS is disabled. Loopback access has no client authentication.

```powershell
codex mcp add rei-editor --url http://127.0.0.1:18777/mcp
```

Reconnect client after changing MCP registration. Restart Editor after rebuilding its assemblies or the native DLL.

Optional launch variables: `REI_EDITOR_STORAGE` isolates Editor preferences; `REI_STARTUP_PROJECT` opens an absolute `.rei` path after window initialization.

## Session and errors

Call `rei_editor_get_state` first:

| Status | Meaning |
| --- | --- |
| `project_management` | Server is available; no project session attached. |
| `project_loading` | Project session exists; current scene is not ready. |
| `ready` | Current project, scene, engine and automation state are available. |

Transport is stateless; tools read the current Editor session on each request. Closing the project detaches the session and cancels its active automation operation. Host startup failure is logged; Editor continues without MCP.

Inputs are validated before mutation. Expected failures become MCP errors in `code: message` form, for example `entity_not_found`, `asset_not_found`, `operation_in_progress` or `capture_unavailable`. Internal exception details are not exposed. Runtime inspection also returns explicit availability statuses, described below.

## Tools

Parameter names below match MCP input schema. `—` means no parameters. Results use structured JSON except `capture_frame`, which returns metadata and PNG content.

### Scene

| Tool | Parameters | Result / effect |
| --- | --- | --- |
| `rei_editor_get_state` | — | Project, scene, engine, import/build flags and active operation. |
| `rei_editor_list_entities` | — | Hierarchy in display order: entity/parent ids, order, depth and attached behaviours. |
| `rei_editor_get_entity` | `entityId` | Behaviour property names, types and JSON-compatible values. |
| `rei_editor_rename_entity` | `entityId`, `newName` | Renames entity. Name must be non-empty, at most 128 characters. |
| `rei_editor_add_behaviour` | `entityId`, `behaviourName` | Adds registered type. Already attached: unchanged success. |
| `rei_editor_set_behaviour_property` | `entityId`, `behaviourName`, `propertyName`, `value` | Changes one serialized property. |

### Assets

| Tool | Parameters | Result / effect |
| --- | --- | --- |
| `rei_editor_select_asset` | `assetId` | Selects asset in Project/Monitor; returns `assetId`, `assetName`, `monitorSupported`. Monitor loading may finish asynchronously. |
| `rei_editor_get_asset_state` | `assetId`, `source` | Independent Editor or native runtime snapshot; see inspection rules below. |
| `rei_editor_set_material_property` | `materialAssetId`, `propertyName`, `value` | Changes one supported shader uniform; validates texture references. |
| `rei_editor_list_data_asset_types` | — | Available types, stable numeric type ids and serialized property schemas. |
| `rei_editor_list_data_assets` | — | Instances with asset ids, native type ids, type names and project-relative paths. |
| `rei_editor_create_data_asset` | `typeName`, `projectPath` | Creates registered type at path relative to Project directory, ending in `.asset`. Existing file: `asset_already_exists`. |
| `rei_editor_get_data_asset` | `assetId` | Type, path, property names/types and JSON-compatible values. |
| `rei_editor_set_data_asset_property` | `assetId`, `propertyName`, `value` | Changes one serialized property; updates loaded native instance. |

### Lifecycle and operations

| Tool | Parameters | Result / effect |
| --- | --- | --- |
| `rei_editor_save_project` | — | Synchronizes scene from engine and saves dirty project assets. |
| `rei_editor_refresh_assets` | — | Starts asset reimport, metadata and behaviour/shader registry refresh; returns operation. |
| `rei_editor_start_build` | Build options below | Starts project solution and/or asset build; returns operation. |
| `rei_editor_start_playmode` | — | Saves, performs incremental `EditorDebug` build and starts Play; returns operation. |
| `rei_editor_stop_playmode` | — | Stops Play and restores Editor mode through existing lifecycle; returns operation. |
| `rei_editor_get_operation` | `operationId` | Status, progress, message, timestamps, log count and safe error. |
| `rei_editor_cancel_operation` | `operationId` | Requests cooperative cancellation. |
| `rei_editor_get_logs` | `operationId=null`, `minimumLevel="debug"`, `limit=100` | Console snapshot or retained operation logs. Levels: `debug`, `info`, `warning`, `error`; limit `1..500`. |
| `rei_editor_capture_frame` | — | Final engine frame after post-processing, UI and debug overlay. Returns `image/png` and `width`, `height`, `capturedAtUtc`, `engineMode`. No disk file is created. |

### CPU profiling

| Tool | Parameters | Result / effect |
| --- | --- | --- |
| `rei_editor_get_profiling_snapshot` | `source="runtime"`, `view="recent"`, `expectedSessionId=null`, `limit=256` | Copies completed-frame native CPU aggregates. Views: `recent`, `last_capture`; limit `1..256`. Does not enable recording. |
| `rei_editor_start_profiling_capture` | `frameCount` | Requests `1..3600` frames; returns status and session/capture ids. No scene/asset edits or save. |

Annotations: read tools are read-only. Mutations are idempotent except `create_data_asset` and `start_profiling_capture`. `capture_frame` is read-only but non-idempotent. `save_project`, `refresh_assets` and `start_build` are marked destructive. All tools have `OpenWorld=false`.

## Property writes and persistence

Use entity ids from `list_entities`, registered type names from type lists, and exact property names from inspection. Material properties use supported shader uniform names.

`value` is JSON-compatible. Custom values accept partial objects: omitted fields retain current values. Asset references use `{"Id":"asset-guid"}`; incompatible referenced types are rejected.

Scene and property edits require explicit `save_project` to persist. Loaded DataAssets update in place; unloaded DataAsset edits need save/reimport before initial native loading. Play-mode DataAsset writes are session-only: after Stop, repeat them in Editor mode and save.

Save is rejected during Play, build, another save or an active automation operation.

## Asset inspection

`get_asset_state` requires `source="editor"` or `"runtime"` and returns `assetId`, `source`, `status`, `values`.

- `editor`: supports DataAsset and Material; may populate Editor asset cache.
- `runtime`: reads native memory without loading assets or falling back to Editor values.

Statuses: `loaded`, `unloaded`, `unsupported`, `engine_unavailable`, `read_failed`, `too_large`. Unknown asset ids produce `asset_not_found`; invalid source produces `invalid_source`. Asset selection can additionally fail with `selection_failed`.

Snapshots represent memory, not disk. Native snapshots are limited to 16 MiB. DataAsset values use plain serialized field names; native snapshots also retain `REI_TYPE` metadata. Compare floating-point values with tolerance; runtime values can change between calls.

## Build options

| Parameter | Default | Meaning |
| --- | --- | --- |
| `configuration` | `"editor_debug"` | `debug`, `editor_debug`, `release` or `editor_release`. |
| `forceSolutionRebuild` | `false` | Ignore solution build cache. |
| `forceCleanSolutionBuild` | `false` | Clean solution outputs; implies forced rebuild. |
| `forceAssetRebuild` | `false` | Ignore asset build cache. |
| `buildSolution` | `true` | Compile project solution. |
| `buildAssets` | `true` | Build project assets. |

At least one of `buildSolution` / `buildAssets` must be enabled. Clean solution build requires `buildSolution=true`.

## Async operations

Refresh, build and Play/Stop return an operation id immediately. Poll `get_operation` until terminal status:

```text
queued -> running -> succeeded | failed | canceled
```

Only one conflicting automation operation can run per Editor session. Last 20 completed operations remain available; each retains up to 1000 log records independently of console clearing.

Cancellation is cooperative: non-cancelable import phases finish first; build propagates cancellation to supported phases. Canceled Play startup schedules engine stop if startup already began.

Typical sequence: check `ready` and no active operation → edit/refresh and await completion → set properties and save → start Play and await completion → capture/read logs → stop Play and await completion. Separate build is optional because start Play already builds.

## Profiling results

Read statuses: `ok`, `engine_unavailable`, `disabled`, `no_samples`, `session_changed`, `unsupported`, `read_failed`. Missing export in the loaded project DLL returns `unsupported`. Invalid inputs produce `invalid_source`, `invalid_view`, `invalid_session_id`, `invalid_limit` or `invalid_frame_count`.

`recent` contains up to 120 frames in a tumbling window. Disabled recording can retain earlier data with status `disabled`. Completed `last_capture` remains readable with status `ok`. `limit` truncates returned metrics, not collection.

Capture starts at the next engine frame boundary. Start returns `queued` with `sessionId` / `captureId`; another request during queued/recording returns `busy` and preserves existing capture. Read `last_capture` for `captureState` (`idle`, `queued`, `recording`, `complete`, `cancelled`), `targetFrames` and `completedFrames`. Results publish after the full preceding frame interval is known.

Stop cancels unfinished capture and destroys its session. Editor-mode restart or DLL reload creates a new session; old captures are not retained. Session/capture ids are decimal strings. Pass returned `sessionId` as `expectedSessionId` to detect session changes.

Times measure CPU wall time, including waits, not GPU time. `durationMs`, `averageFrameMs`, `maxFrameMs` and `fps` use full frame-start intervals. Scope inclusive time includes children; exclusive time subtracts direct children. Scope `maxCallMs` measures one call; scope `maxFrameMs` measures its per-frame inclusive sum. Check `sampleFrames`, `invalidFrames`, `completeData`: invalid frames advance capture but contribute no metric samples; averages use valid sample frames.

Draw counters describe engine submissions, excluding ImGui and direct project GL calls outside Rei helpers. Submitted vertices count references multiplied by instances; triangles count submitted topology, not visible pixels. UI glyphs count quads; text draws count batches. Property writes count setter attempts; uniform uploads count actual calls.

To compare captures: keep scene/camera/mode/resolution and build settings fixed, warm up, start capture, then poll `last_capture` with expected session id until complete. Validate capture id and `completeData`; avoid builds, frame captures and frequent polling during collection.

## Bridge maintenance

Request path: MCP host → typed tools → `IReiEditorGateway` → UI dispatcher → current session/business services. Long conflicting mutations use the operation coordinator.

When adding a tool:

1. Define narrow contracts and gateway capability; reuse the existing Editor service.
2. Validate inputs, readiness and lifecycle; return safe errors and accurate tool annotations.
3. Add transport/contract tests. Native synchronization requires independent real-engine reads and separate persistence checks.
4. Update this document with parameters, outputs, effects and failures.

Tool definitions: [ReiEditorMcpTools.cs](../ReiEditor.Mcp/Tools/ReiEditorMcpTools.cs). Output schemas: [Contracts](../ReiEditor.Mcp/Contracts). Transport tests run against loopback Kestrel on an ephemeral port:

```powershell
dotnet test ReiEditor.Mcp.Tests/ReiEditor.Mcp.Tests.csproj -c Debug -p:Platform=x64
```

Real-engine prerequisites and commands: [Rei.EngineIntegration.Tests](../Rei.EngineIntegration.Tests/README.md).
