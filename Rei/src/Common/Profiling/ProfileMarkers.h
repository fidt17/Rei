#pragma once
#include "ProfilingService.h"

namespace rei::profiling::markers
{
    inline constexpr auto WINDOW = MakeScope("Rei.Window.Input");
    inline constexpr auto UPDATE = MakeScope("Rei.Update");
    inline constexpr auto BEHAVIOURS = MakeScope("Rei.Behaviours.Update");
    inline constexpr auto APP = MakeScope("Rei.App.Update");
    inline constexpr auto DIAGNOSTICS = MakeScope("Rei.Diagnostics.Sample");
    inline constexpr auto PICK_3D = MakeScope("Rei.Picking.3D");
    inline constexpr auto PICK_UI = MakeScope("Rei.Picking.UI");
    inline constexpr auto TASKS = MakeScope("Rei.Tasks.Execute");
    inline constexpr auto RENDER = MakeScope("Rei.Render");
    inline constexpr auto PREPARE = MakeScope("Rei.Render.Prepare");
    inline constexpr auto SCENE = MakeScope("Rei.Render.Scene");
    inline constexpr auto UI = MakeScope("Rei.UI.Render");
    inline constexpr auto UI_COLLECT = MakeScope("Rei.UI.Collect");
    inline constexpr auto UI_IMAGE = MakeScope("Rei.UI.Image");
    inline constexpr auto UI_TEXT = MakeScope("Rei.UI.Text");
    inline constexpr auto MATERIAL = MakeScope("Rei.Material.Bind");
    inline constexpr auto OVERLAY = MakeScope("Rei.Diagnostics.Overlay");
    inline constexpr auto CAPTURE = MakeScope("Rei.Render.Readback");
    inline constexpr auto SWAP = MakeScope("Rei.Render.Swap");
    inline constexpr auto DRAW_CALLS = MakeCounter("Rei.Draw.Calls");
    inline constexpr auto VERTICES = MakeCounter("Rei.Draw.SubmittedVertices");
    inline constexpr auto TRIANGLES = MakeCounter("Rei.Draw.Triangles");
    inline constexpr auto MATERIAL_BINDS = MakeCounter("Rei.Material.Bindings");
    inline constexpr auto PROPERTY_WRITES = MakeCounter("Rei.Material.PropertyWrites");
    inline constexpr auto UNIFORMS = MakeCounter("Rei.Shader.UniformUploads");
    inline constexpr auto TEXTURES = MakeCounter("Rei.Texture.Bindings");
    inline constexpr auto GLYPHS = MakeCounter("Rei.UI.Glyphs");
    inline constexpr auto UI_TEXT_DRAWS = MakeCounter("Rei.UI.TextDraws");
    inline constexpr auto UI_ITEMS = MakeCounter("Rei.UI.Items");
    inline constexpr auto PICK_CANDIDATES = MakeCounter("Rei.Picking.Candidates");
    inline constexpr auto TASK_COUNT = MakeCounter("Rei.Tasks.Count");

    inline constexpr std::array ALL = {WINDOW, UPDATE, BEHAVIOURS, APP, DIAGNOSTICS, PICK_3D, PICK_UI, TASKS,
        RENDER, PREPARE, SCENE, UI, UI_COLLECT, UI_IMAGE, UI_TEXT, MATERIAL, OVERLAY, CAPTURE, SWAP,
        DRAW_CALLS, VERTICES, TRIANGLES, MATERIAL_BINDS, PROPERTY_WRITES, UNIFORMS, TEXTURES, GLYPHS,
        UI_TEXT_DRAWS, UI_ITEMS, PICK_CANDIDATES, TASK_COUNT};
}
