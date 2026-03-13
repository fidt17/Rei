namespace ReiEditor.Models.Services.Assets.Scripting;

public static class EngineBehavioursConstants
{
    public const string TRANSFORM = "Transform";
    public const string TRANSFORM_POSITION = "_position";
    public const string TRANSFORM_ROTATION = "_rotation";
    public const string TRANSFORM_SCALE = "_scale";
    public const string TRANSFORM_PARENT = "_parent";
    public const string TRANSFORM_ORDER = "_order";
    
    public const string CAMERA = "Camera";
    public const string CAMERA_BACKGROUND_COLOR = "_backgroundColor";
    
    public const string AMBIENT_LIGHT = "AmbientLight";
    public const string POINT_LIGHT = "PointLight";

    public const string MESH_RENDERER = "MeshRenderer";
    public const string MESH_RENDERER_MODEL = "_model";
    public const string MESH_RENDERER_MATERIAL = "_material";

    public const string SPRITE_RENDERER = "SpriteRenderer";
    public const string SPRITE_RENDERER_SPRITE = "_sprite";
    
    public const string ASSET_REF_ID = "Id";

    public const string DEFAULT_ENGINE_SIMPLE_LIT_MATERIAL_ASSET_ID = "rei_simple_lit.mat";
}
