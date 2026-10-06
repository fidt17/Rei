using ReiEditor.Models.Resources;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Assets.DataAssets;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using Property = ReiEditor.Models.Services.Assets.Scripting.Serialization.SerializableObjectInfo.SerializedPropertyData;

if (args.Length != 2) throw new ArgumentException("Expected Rei project directory and generated output directory.");
var reiRoot = Path.GetFullPath(args[0]);
var output = Path.GetFullPath(args[1]);
var probeHeader = Path.Combine(reiRoot, "tests", "support", "BehaviourTestProbes.h");
Property Scalar(SerializedTypeEnum type, string source) => new(type, source, null, SerializedTypeEnum.Invalid, null, null, null, false);
Property Custom(string source) => Scalar(SerializedTypeEnum.Custom, source);
Property Collection(string itemSource, SerializedTypeEnum itemType) => new(SerializedTypeEnum.Collection, $"std::vector<{itemSource}>", itemSource, itemType, itemSource, null, null, false);
SerializableObjectInfo Object(string ns, string name, Dictionary<string, Property> properties, string? header = null, bool template = false) => new(ns, name, template, new ObjectFile<string>("", header ?? probeHeader), properties, header ?? probeHeader);
var objects = new List<SerializableObjectInfo>
{
    Object("rei::render", "RendererSettings", new() { ["_exposureEV"] = Scalar(SerializedTypeEnum.Float, "f32"), ["_toneMapping"] = Scalar(SerializedTypeEnum.Enum, "rei::render::ToneMappingMode"), ["_maxPointLights"] = Scalar(SerializedTypeEnum.Integer, "i32") }, Path.Combine(reiRoot, "resources", "rei_data_assets", "render", "RendererSettings.h")),
    Object("rei::tests", "ProbeNested", new() { ["Value"] = Scalar(SerializedTypeEnum.Integer, "i32"), ["Label"] = Scalar(SerializedTypeEnum.String, "std::string") }),
    Object("rei::tests", "ProbeDataAsset", new() { ["Number"] = Scalar(SerializedTypeEnum.Integer, "i32"), ["Label"] = Scalar(SerializedTypeEnum.String, "std::string"), ["Settings"] = Custom("rei::tests::ProbeNested"), ["Values"] = Collection("i32", SerializedTypeEnum.Integer) }),
    Object("rei::math", "Vector3", new() { ["x"] = Scalar(SerializedTypeEnum.Float, "f32"), ["y"] = Scalar(SerializedTypeEnum.Float, "f32"), ["z"] = Scalar(SerializedTypeEnum.Float, "f32") }, Path.Combine(reiRoot, "src", "Common", "Math", "Vector3.h")),
    Object("rei::math", "Vector2", new() { ["x"] = Scalar(SerializedTypeEnum.Float, "f32"), ["y"] = Scalar(SerializedTypeEnum.Float, "f32") }, Path.Combine(reiRoot, "src", "Common", "Math", "Vector2.h")),
    Object("rei::render", "Color", new() { ["r"] = Scalar(SerializedTypeEnum.Float, "f32"), ["g"] = Scalar(SerializedTypeEnum.Float, "f32"), ["b"] = Scalar(SerializedTypeEnum.Float, "f32"), ["a"] = Scalar(SerializedTypeEnum.Float, "f32") }, Path.Combine(reiRoot, "src", "Modules", "Render", "Color", "Color.h")),
    Object("rei::assets", "AssetRef", new() { ["Id"] = Scalar(SerializedTypeEnum.String, "std::string") }, Path.Combine(reiRoot, "src", "Modules", "Assets", "Core", "AssetRef.h"), true),
    Object("rei::ecs", "ComponentRef", new() { ["SceneEntityId"] = Scalar(SerializedTypeEnum.Integer, "i32") }, Path.Combine(reiRoot, "src", "Ecs", "ComponentRef.h"), true)
};
BehaviourAssetInfo Behaviour(string name, int id, Dictionary<string, Property> properties, string[]? required = null) => new("rei::tests", name, id, new ObjectFile<string>("", probeHeader), properties, required ?? [], probeHeader);
var behaviours = new Dictionary<int, BehaviourAssetInfo>
{
    [7101] = Behaviour("ProbeA", 7101, new()
    {
        ["Number"] = Scalar(SerializedTypeEnum.Integer, "i32"), ["Ratio"] = Scalar(SerializedTypeEnum.Float, "f32"),
        ["Flag"] = Scalar(SerializedTypeEnum.Boolean, "bool"), ["Text"] = Scalar(SerializedTypeEnum.String, "std::string"),
        ["Mode"] = Scalar(SerializedTypeEnum.Enum, "rei::tests::ProbeMode"), ["Settings"] = Custom("rei::tests::ProbeNested"),
        ["Values"] = Collection("i32", SerializedTypeEnum.Integer), ["Modes"] = Collection("rei::tests::ProbeMode", SerializedTypeEnum.Enum),
        ["Items"] = Collection("rei::tests::ProbeNested", SerializedTypeEnum.Custom),
        ["Asset"] = Custom("rei::assets::AssetRef<rei::tests::ProbeDataAsset>"), ["Assets"] = Collection("rei::assets::AssetRef<rei::tests::ProbeDataAsset>", SerializedTypeEnum.Custom),
        ["Target"] = Custom("rei::ecs::ComponentRef<rei::Transform>"), ["Targets"] = Collection("rei::ecs::ComponentRef<rei::Transform>", SerializedTypeEnum.Custom)
    }),
    [7102] = Behaviour("ProbeB", 7102, new() { ["Number"] = Scalar(SerializedTypeEnum.Integer, "i32") }),
    [7103] = Behaviour("ProbeC", 7103, new(), ["ProbeB"]),
    [7104] = Behaviour("ProbeD", 7104, new(), ["ProbeB"])
};
var transformHeader = Path.Combine(reiRoot, "resources", "rei_behaviours", "transformation", "Transform.h");
behaviours.Add(7199, new BehaviourAssetInfo("rei", "Transform", 7199, new ObjectFile<string>("", transformHeader), new()
{
    ["_position"] = Custom("rei::math::Vector3"), ["_rotation"] = Custom("rei::math::Vector3"), ["_scale"] = Custom("rei::math::Vector3"),
    ["_parent"] = Scalar(SerializedTypeEnum.Integer, "i32"), ["_order"] = Scalar(SerializedTypeEnum.Integer, "i32")
}, [], transformHeader));
void AddEngineBehaviour(string ns, string name, int id, string relativeHeader, Dictionary<string, Property> properties, string[]? required = null)
{
    var header = Path.Combine(reiRoot, "resources", "rei_behaviours", relativeHeader);
    behaviours.Add(id, new BehaviourAssetInfo(ns, name, id, new ObjectFile<string>("", header), properties, required ?? [], header));
}
AddEngineBehaviour("rei::render", "Camera", 7301, "render/camera/Camera.h", new()
{
    ["_fov"] = Scalar(SerializedTypeEnum.Float, "f32"), ["_orthographicSize"] = Scalar(SerializedTypeEnum.Float, "f32"),
    ["_nearClipPlane"] = Scalar(SerializedTypeEnum.Integer, "i32"), ["_farClipPlane"] = Scalar(SerializedTypeEnum.Integer, "i32"),
    ["_backgroundColor"] = Custom("rei::render::Color"), ["_perspective"] = Scalar(SerializedTypeEnum.Enum, "rei::render::CameraPerspectiveEnum"),
    ["_rendererSettings"] = Custom("rei::assets::AssetRef<rei::render::RendererSettings>")
});
AddEngineBehaviour("rei::ui", "RectTransform", 7303, "ui/RectTransform.h", new()
{
    ["_anchorMin"] = Custom("rei::math::Vector2"), ["_anchorMax"] = Custom("rei::math::Vector2"), ["_pivot"] = Custom("rei::math::Vector2"),
    ["_anchoredPosition"] = Custom("rei::math::Vector2"), ["_sizeDelta"] = Custom("rei::math::Vector2")
});
AddEngineBehaviour("rei::ui", "Canvas", 7302, "ui/Canvas.h", new()
{
    ["_referenceResolution"] = Custom("rei::math::Vector2"), ["_scaleMode"] = Scalar(SerializedTypeEnum.Enum, "rei::ui::CanvasScaleMode"),
    ["_matchWidthOrHeight"] = Scalar(SerializedTypeEnum.Float, "f32"), ["_pixelsPerUnit"] = Scalar(SerializedTypeEnum.Float, "f32")
}, ["RectTransform"]);
AddEngineBehaviour("rei::ui", "Image", 7304, "ui/Image.h", new()
{
    ["_texture"] = Custom("rei::assets::AssetRef<rei::render::Texture>"), ["_color"] = Custom("rei::render::Color"),
    ["_preserveAspect"] = Scalar(SerializedTypeEnum.Boolean, "bool"), ["_raycastTarget"] = Scalar(SerializedTypeEnum.Boolean, "bool")
}, ["RectTransform"]);
AddEngineBehaviour("rei::ui", "Text", 7305, "ui/Text.h", new()
{
    ["_value"] = Scalar(SerializedTypeEnum.String, "std::string"), ["_font"] = Custom("rei::assets::AssetRef<rei::render::Font>"), ["_color"] = Custom("rei::render::Color"),
    ["_size"] = Scalar(SerializedTypeEnum.Float, "f32"), ["_autoSize"] = Scalar(SerializedTypeEnum.Boolean, "bool"), ["_raycastTarget"] = Scalar(SerializedTypeEnum.Boolean, "bool")
}, ["RectTransform"]);
AddEngineBehaviour("rei::render", "PointLight", 7306, "render/light/PointLight.h", new()
{
    ["_strength"] = Scalar(SerializedTypeEnum.Float, "f32"), ["_range"] = Scalar(SerializedTypeEnum.Float, "f32"), ["_color"] = Custom("rei::render::Color")
});
AddEngineBehaviour("rei::render", "AmbientLight", 7307, "render/light/AmbientLight.h", new()
{
    ["_strength"] = Scalar(SerializedTypeEnum.Float, "f32"), ["_color"] = Custom("rei::render::Color")
});
AddEngineBehaviour("rei::render", "MeshRenderer", 7308, "render/MeshRenderer.h", new()
{
    ["_model"] = Custom("rei::assets::AssetRef<rei::render::Model>"), ["_material"] = Custom("rei::assets::AssetRef<rei::render::Material>")
});
AddEngineBehaviour("rei::render", "SpriteRenderer", 7309, "render/SpriteRenderer.h", new()
{
    ["_color"] = Custom("rei::render::Color"), ["_sprite"] = Custom("rei::assets::AssetRef<rei::render::Texture>")
});
AddEngineBehaviour("rei::ui", "Button", 7310, "ui/Button.h", new()
{
    ["_targetImage"] = Custom("rei::ecs::ComponentRef<rei::ui::Image>"), ["_interactable"] = Scalar(SerializedTypeEnum.Boolean, "bool"),
    ["_changeImageColor"] = Scalar(SerializedTypeEnum.Boolean, "bool"), ["_normalColor"] = Custom("rei::render::Color"),
    ["_hoverColor"] = Custom("rei::render::Color"), ["_pressedColor"] = Custom("rei::render::Color"), ["_disabledColor"] = Custom("rei::render::Color")
}, ["RectTransform", "Image"]);
var generator = new BehaviourRegistrySourceGenerator(new OutputResources(output), new FixtureRegistry(objects));
await generator.GenerateBehaviourRegistrySourceFile(behaviours, objects, [new DataAssetTypeInfo(7201, objects.Single(x => x.ObjectName == "ProbeDataAsset")), new DataAssetTypeInfo(7202, objects.Single(x => x.ObjectName == "RendererSettings"))]);

sealed class FixtureRegistry(List<SerializableObjectInfo> objects) : ISerializableObjectsRegistry
{
    public IEnumerable<SerializableObjectInfo> GetObjects() => objects;
    public SerializableObjectInfo? GetObject(string name) => objects.Find(x => x.ObjectName == name.Split('<')[0]);
    public SerializableEnum? GetEnum(string name) => null;
    public void Replace(IEnumerable<SerializableObjectInfo> values, IEnumerable<SerializableEnum> enums) => throw new NotSupportedException();
}

sealed class OutputResources(string output) : IResourceService
{
    public string GetRootPath(params string[] path) => Path.Combine([output, .. path]);
    public string GetProjectPath(params string[] path) => Path.Combine(output, "BehaviourRegistry.cpp");
    public string GetScriptsPath(params string[] path) => GetRootPath(path);
    public IEnumerable<string> GetAllWithExtension(string extension) => [];
    public void CopyFilesRecursively(string source, string target) => throw new NotSupportedException();
    public void MoveFilesRecursively(string source, string target) => throw new NotSupportedException();
    public Task<T> Load<T>(string fullPath) => throw new NotSupportedException();
    public Task<T?> TryLoad<T>(string fullPath) => throw new NotSupportedException();
    public bool Exists(string fullPath) => File.Exists(fullPath);
    public async Task<bool> Write(string data, string fullPath)
    {
        Directory.CreateDirectory(output);
        if (!File.Exists(fullPath) || await File.ReadAllTextAsync(fullPath) != data) await File.WriteAllTextAsync(fullPath, data);
        return true;
    }
}
