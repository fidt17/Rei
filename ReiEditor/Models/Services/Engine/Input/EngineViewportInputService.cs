using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ReiEditor.Mcp.Contracts;
using ReiEditor.Models.Services.Engine.Playmode;

namespace ReiEditor.Models.Services.Engine.Input;

public sealed class EngineViewportInputService(IEngineNativeAccess native) : IEngineViewportInputService
{
    private delegate int SendDelegate(string requestJson);
    private const int MAX_EVENTS = 128;
    private const int DRAG_STEPS = 8;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ReiViewportInputResult> SendAsync(IReadOnlyList<ReiViewportInputEvent> events, CancellationToken cancellationToken)
    {
        if (events == null || events.Count is < 1 or > MAX_EVENTS)
            throw new ReiMcpOperationException("invalid_input", "Input requires 1-128 events.");
        foreach (var item in events)
        {
            if (item == null || !double.IsFinite(item.X) || !double.IsFinite(item.Y) ||
                item.Type is not ("move" or "button_down" or "button_up" or "key_down" or "key_up" or "scroll" or "wait") ||
                (item.Type == "move" && (item.X < 0 || item.Y < 0)) ||
                ((item.Type is "button_down" or "button_up") && (item.Code is < 0 or > 2)) ||
                ((item.Type is "key_down" or "key_up") && (item.Code is < 32 or > 347)))
                throw new ReiMcpOperationException("invalid_input", "Invalid event type, coordinates, or GLFW key/button code.");
        }
        var json = JsonSerializer.Serialize(events, JsonOptions);
        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!native.TryInvoke(api =>
            {
                if (!api.HasExport("SimulateEditorInput")) throw new ReiMcpOperationException("input_unsupported", "Rebuild project DLL to enable viewport input.");
                var result = api.Invoke<int>(typeof(SendDelegate), "SimulateEditorInput", json);
                if (result != 1) throw new ReiMcpOperationException(result switch
                {
                    -1 => "input_unavailable", -2 => "input_busy", -3 => "invalid_input", -4 => "input_timeout", _ => "input_failed"
                }, "Viewport input did not complete. Partial input may already have been applied; inspect state before retrying.");
            })) throw new ReiMcpOperationException("engine_unavailable", "Engine is not running.");
        }, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return new("completed", events.Count);
    }

    public Task<ReiViewportInputResult> DragAsync(double fromX, double fromY, double toX, double toY, CancellationToken cancellationToken)
    {
        var events = new List<ReiViewportInputEvent> { new("move", fromX, fromY), new("button_down") };
        for (var step = 1; step <= DRAG_STEPS; step++)
            events.Add(new("move", fromX + (toX - fromX) * step / DRAG_STEPS, fromY + (toY - fromY) * step / DRAG_STEPS));
        events.Add(new("button_up"));
        return SendAsync(events, cancellationToken);
    }
}
