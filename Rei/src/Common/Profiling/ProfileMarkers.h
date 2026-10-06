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
    inline constexpr auto PICK_SELECTION = MakeScope("Rei.Picking.Selection");
    inline constexpr auto PICK_UI = MakeScope("Rei.Picking.UI");
    inline constexpr auto TASKS = MakeScope("Rei.Tasks.Execute");
    inline constexpr auto RENDER = MakeScope("Rei.Render");
    inline constexpr auto PREPARE = MakeScope("Rei.Render.Prepare");
    inline constexpr auto SCENE = MakeScope("Rei.Render.Scene");
    inline constexpr auto OUTLINE_PASS = MakeScope("Rei.Render.Outline.Pass");
    inline constexpr auto GEOMETRY = MakeScope("Rei.Render.Geometry");
    inline constexpr auto LIGHTING_APPLY = MakeScope("Rei.Render.Lighting.Apply");
    inline constexpr auto OBJECT_DATA = MakeScope("Rei.Render.ObjectData");
    inline constexpr auto MESH_SUBMIT = MakeScope("Rei.Render.Mesh.Submit");
    inline constexpr auto HELPERS = MakeScope("Rei.Render.Helpers");
    inline constexpr auto OUTPUT = MakeScope("Rei.Render.Output");
    inline constexpr auto OUTLINE_COMPOSITE = MakeScope("Rei.Render.Outline.Composite");
    inline constexpr auto UI = MakeScope("Rei.UI.Render");
    inline constexpr auto UI_COLLECT = MakeScope("Rei.UI.Collect");
    inline constexpr auto UI_IMAGE = MakeScope("Rei.UI.Image");
    inline constexpr auto UI_TEXT = MakeScope("Rei.UI.Text");
    inline constexpr auto UI_TEXT_LAYOUT = MakeScope("Rei.UI.Text.Layout");
    inline constexpr auto UI_TEXT_MEASURE = MakeScope("Rei.UI.Text.Measure");
    inline constexpr auto UI_TEXT_GEOMETRY = MakeScope("Rei.UI.Text.Geometry");
    inline constexpr auto UI_TEXT_SUBMIT = MakeScope("Rei.UI.Text.Submit");
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
    inline constexpr auto SHADER_USE_CALLS = MakeCounter("Rei.Shader.UseCalls");
    inline constexpr auto UNIFORMS_LIGHTING = MakeCounter("Rei.Shader.UniformUploads.Lighting");
    inline constexpr auto UNIFORMS_CAMERA = MakeCounter("Rei.Shader.UniformUploads.Camera");
    inline constexpr auto UNIFORMS_OBJECT = MakeCounter("Rei.Shader.UniformUploads.Object");
    inline constexpr auto UNIFORMS_MATERIAL = MakeCounter("Rei.Shader.UniformUploads.Material");
    inline constexpr auto UNIFORMS_OTHER = MakeCounter("Rei.Shader.UniformUploads.Other");
    inline constexpr auto TEXTURES = MakeCounter("Rei.Texture.Bindings");
    inline constexpr auto GLYPHS = MakeCounter("Rei.UI.Glyphs");
    inline constexpr auto UI_TEXT_DRAWS = MakeCounter("Rei.UI.TextDraws");
    inline constexpr auto UI_ITEMS = MakeCounter("Rei.UI.Items");
    inline constexpr auto PICK_CANDIDATES = MakeCounter("Rei.Picking.Candidates");
    inline constexpr auto PICK_SELECTION_CANDIDATES = MakeCounter("Rei.Picking.SelectionCandidates");
    inline constexpr auto TASK_COUNT = MakeCounter("Rei.Tasks.Count");

    inline constexpr std::array ALL = {WINDOW, UPDATE, BEHAVIOURS, APP, DIAGNOSTICS, PICK_3D, PICK_SELECTION, PICK_UI, TASKS,
        RENDER, PREPARE, SCENE, OUTLINE_PASS, GEOMETRY, LIGHTING_APPLY, OBJECT_DATA, MESH_SUBMIT, HELPERS, OUTPUT, OUTLINE_COMPOSITE,
        UI, UI_COLLECT, UI_IMAGE, UI_TEXT, UI_TEXT_LAYOUT, UI_TEXT_MEASURE, UI_TEXT_GEOMETRY, UI_TEXT_SUBMIT,
        MATERIAL, OVERLAY, CAPTURE, SWAP,
        DRAW_CALLS, VERTICES, TRIANGLES, MATERIAL_BINDS, PROPERTY_WRITES, UNIFORMS, SHADER_USE_CALLS,
        UNIFORMS_LIGHTING, UNIFORMS_CAMERA, UNIFORMS_OBJECT, UNIFORMS_MATERIAL, UNIFORMS_OTHER, TEXTURES, GLYPHS,
        UI_TEXT_DRAWS, UI_ITEMS, PICK_CANDIDATES, PICK_SELECTION_CANDIDATES, TASK_COUNT};
    static_assert(ALL.size() <= MAX_METRICS, "Built-in markers must fit the profiler registry.");
}
