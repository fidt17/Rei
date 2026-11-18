#include "Startup/AppEntryPoint.h"
#include "Common/Math/math.h"
#include "Common/Transform/RectTransformUtility.h"
#include "Modules/Editor/TransformationControls/TransformationControl.h"
#include "Modules/Physics/PointerCollisionListener.h"
#include "rei_behaviours/ui/Canvas.h"
#include "rei_behaviours/ui/RectTransform.h"
#include <fstream>

// Test-project setup/readback only. All gestures and history traversal use MCP.
class TestApplication final : public rei::App
{
    struct Part { std::string Name; rei::ecs::Entity Entity; bool Active; };
    i32 _step = 0;
    std::vector<rei::ecs::Entity> _targets;
    std::string _part, _lastActive;
    bool _rect = false;

    std::vector<Part> Parts(const rei::editor::TransformationControl& c) const
    {
        using namespace rei::editor;
        if (c.Mode == Movement)
        {
            std::vector<Part> parts = {{"move_xy", c.RightUpMovementPlane.Entity, c.RightUpMovementPlane.DragActive}};
            if (!_rect)
            {
                parts.push_back({"move_xz", c.RightForwardMovementPlane.Entity, c.RightForwardMovementPlane.DragActive});
                parts.push_back({"move_yz", c.UpForwardMovementPlane.Entity, c.UpForwardMovementPlane.DragActive});
            }
            parts.push_back({"move_x", c.RightMovementArrow.Entity, c.RightMovementArrow.DragActive});
            parts.push_back({"move_y", c.UpMovementArrow.Entity, c.UpMovementArrow.DragActive});
            if (!_rect) parts.push_back({"move_z", c.ForwardMovementArrow.Entity, c.ForwardMovementArrow.DragActive});
            return parts;
        }
        if (c.Mode == Scale) return {{"scale_all", c.RootScale.Entity, c.RootScale.DragActive}, {"scale_z", c.ForwardScaleArrow.Entity, c.ForwardScaleArrow.DragActive},
            {"scale_x", c.RightScaleArrow.Entity, c.RightScaleArrow.DragActive}, {"scale_y", c.UpScaleArrow.Entity, c.UpScaleArrow.DragActive}};
        if (c.Mode == Rotation) return {{"rotate_x", c.RightRotationRing.Entity, c.RightRotationRing.DragActive}, {"rotate_y", c.UpRotationRing.Entity, c.UpRotationRing.DragActive},
            {"rotate_z", c.ForwardRotationRing.Entity, c.ForwardRotationRing.DragActive}};
        return {{"rect_tl", c.TopLeftRectHandle.Entity, c.TopLeftRectHandle.DragActive}, {"rect_t", c.TopRectHandle.Entity, c.TopRectHandle.DragActive},
            {"rect_tr", c.TopRightRectHandle.Entity, c.TopRightRectHandle.DragActive}, {"rect_l", c.LeftRectHandle.Entity, c.LeftRectHandle.DragActive},
            {"rect_r", c.RightRectHandle.Entity, c.RightRectHandle.DragActive}, {"rect_bl", c.BottomLeftRectHandle.Entity, c.BottomLeftRectHandle.DragActive},
            {"rect_b", c.BottomRectHandle.Entity, c.BottomRectHandle.DragActive}, {"rect_br", c.BottomRightRectHandle.Entity, c.BottomRightRectHandle.DragActive}};
    }

    void Configure(const nlohmann::json& command)
    {
        ECS_WORLD(rei::GetInternalWorld());
        // Keep previous unselected cases alive until harness teardown: IDs stay unique.
        _targets.clear();
        _lastActive.clear();
        _rect = command.at("Rect");
        _part = command.at("Part");
        const auto camera = rei::render::Camera::GetMainCamera();
        camera.Get().GetTransform().GetLocalPosition() = {6, 5, -10};
        camera.Get().GetTransform().SetRotation(rei::math::Vector3(23, -31, 0));
        auto canvas = rei::ecs::NULL_ENTITY;
        if (_rect)
        {
            canvas = rei::GetEntityManager().CreateNewEntity("MCP canvas");
            auto& c = ADD_BEHAVIOUR(canvas, rei::ui::Canvas);
            const nlohmann::json data = {{"_scaleMode", {{"Value", 0}}}};
            c.REI_SET(data);
        }
        const i32 count = command.at("Count");
        const auto mouse = rei::Input::GetMouseState();
        for (i32 i = 0; i < count; i++)
        {
            const auto e = rei::GetEntityManager().CreateNewEntity("MCP target " + std::to_string(i));
            _targets.push_back(e);
            auto& t = GET(e, rei::Transform);
            if (!_rect)
            {
                t.GetLocalPosition() = {count == 1 ? 0.0f : (i == 0 ? -1.0f : 1.0f), static_cast<f32>(i) * 0.5f, 0};
            }
            else
            {
                t.SetParent(canvas);
                auto& r = ADD_BEHAVIOUR(e, rei::ui::RectTransform);
                r.GetAnchoredPosition() = {count == 1 ? 0.0f : (i == 0 ? -130.0f : 130.0f), static_cast<f32>(i) * 40};
                r.GetSizeDelta() = {200, 160};
                if (command.at("Stretch").get<bool>())
                {
                    r.GetAnchorMin() = {0.25f, 0.25f};
                    r.GetAnchorMax() = {0.75f, 0.75f};
                    r.GetSizeDelta() = {200 - mouse.Width * 0.5f, 160 - mouse.Height * 0.5f};
                }
            }
        }
        rei::GetInternalWorld()->Refresh();
    }

    nlohmann::json Pointer(const rei::editor::TransformationControl& control, const std::vector<Part>& parts) const
    {
        ECS_WORLD(rei::GetInternalWorld());
        const auto camera = rei::render::Camera::GetMainCamera();
        const auto mouse = rei::Input::GetMouseState();
        if (!control.HasTargets() || camera.IsNull()) return nullptr;
        if (_part == "body")
        {
            const auto e = control.PrimaryTargetEntity;
            const auto canvas = rei::ui_utility::FindCanvasEntity(e);
            const auto bounds = rei::ui_utility::CalculateRect(e, canvas, mouse.Width, mouse.Height);
            return {bounds.Min.x + (bounds.Max.x - bounds.Min.x) * 0.3f, mouse.Height - bounds.Min.y - (bounds.Max.y - bounds.Min.y) * 0.3f};
        }
        const auto selected = std::find_if(parts.begin(), parts.end(), [&](const auto& p) { return p.Name == _part; });
        if (selected == parts.end()) return nullptr;
        const bool ring = control.Mode == rei::editor::Rotation;
        const bool handle = control.Mode == rei::editor::RectTransform;
        const bool plane = _part == "move_xy" || _part == "move_xz" || _part == "move_yz";
        glm::vec4 local(0, 0, 0, 1);
        if (plane) local = {0.53f, 0.53f, 0, 1};
        else if (control.Mode == rei::editor::Movement) local.z = 3.5f;
        else if (control.Mode == rei::editor::Scale && _part != "scale_all") local.z = 4;
        const auto model = GET(selected->Entity, rei::Transform).CalculateWorldModelMatrix();
        const auto center = ring ? rei::math::Vector3(control.PivotScreenPosition.x, control.PivotScreenPosition.y, 0)
            : camera.Get().WorldToScreenPosition(rei::math::Vector3(glm::vec3(model * local)));
        const i32 radius = ring ? 155 : handle ? 10 : 16;
        auto hits = [&](const Part& part, f32 x, f32 y)
        {
            if (IS_DEAD(part.Entity) || !HAS(part.Entity, rei::physics::PointerCollisionListener)) return false;
            const auto& collider = GET(part.Entity, rei::physics::PointerCollisionListener).Collider;
            rei::math::Vector3 point;
            return collider && collider->Intersect(camera.Get().GetScreenPointToRay(x, y), GET(part.Entity, rei::Transform).CalculateWorldModelMatrix(), point);
        };
        nlohmann::json result = nullptr;
        f32 best = -1;
        for (i32 dy = -radius; dy <= radius; dy += 2)
        for (i32 dx = -radius; dx <= radius; dx += 2)
        {
            const f32 x = center.x + dx, y = center.y + dy;
            if (x < 4 || y < 4 || x >= mouse.Width - 40 || y >= mouse.Height - 40 || !hits(*selected, x, y)) continue;
            bool blocked = false;
            for (auto it = parts.begin(); it != selected; ++it) if (hits(*it, x, y)) { blocked = true; break; }
            if (blocked) continue;
            if (!ring && (!hits(*selected, x - 1, y) || !hits(*selected, x + 1, y) || !hits(*selected, x, y - 1) || !hits(*selected, x, y + 1))) continue;
            const f32 score = static_cast<f32>(dx * dx + dy * dy);
            if (score <= best) continue;
            best = score;
            result = {x, y};
        }
        return result;
    }

    void Publish(const nlohmann::json& command, const rei::editor::TransformationControl& control, const std::vector<Part>& parts)
    {
        ECS_WORLD(rei::GetInternalWorld());
        auto entities = nlohmann::json::array();
        for (const auto e : _targets)
        {
            auto& t = GET(e, rei::Transform);
            auto fields = nlohmann::json::object();
            auto add3 = [&](const char* name, const rei::math::Vector3& v) { fields[name] = {v.x, v.y, v.z}; };
            add3("_position", t.GetLocalPosition());
            add3("_rotation", rei::math::GetEulerAngles(t.GetLocalRotation()));
            add3("_scale", t.GetLocalScale());
            nlohmann::json bounds = nullptr;
            if (_rect)
            {
                const auto& r = GET(e, rei::ui::RectTransform);
                auto add2 = [&](const char* name, const rei::math::Vector2& v) { fields[name] = {v.x, v.y}; };
                add2("_anchoredPosition", r.GetAnchoredPosition());
                add2("_sizeDelta", r.GetSizeDelta());
                add2("_anchorMin", r.GetAnchorMin());
                add2("_anchorMax", r.GetAnchorMax());
                add2("_pivot", r.GetPivot());
                const auto mouse = rei::Input::GetMouseState();
                const auto rect = rei::ui_utility::CalculateRect(e, rei::ui_utility::FindCanvasEntity(e), mouse.Width, mouse.Height);
                bounds = {rect.Min.x, rect.Min.y, rect.Max.x, rect.Max.y};
            }
            entities.push_back({{"Id", GET(e, EntityInfo).Id}, {"Fields", fields}, {"Bounds", bounds}});
        }
        const auto mouse = rei::Input::GetMouseState();
        auto selected = nlohmann::json::array();
        for (const auto e : control.TargetEntities) if (!IS_DEAD(e)) selected.push_back(GET(e, EntityInfo).Id);
        const nlohmann::json result = {{"Step", _step}, {"Width", mouse.Width}, {"Height", mouse.Height}, {"Mode", static_cast<i32>(control.Mode)},
            {"Selected", selected}, {"RectTargets", control.HasRectTransformTargets},
            {"Pivot", {control.PivotScreenPosition.x, control.PivotScreenPosition.y}}, {"Hit", command.value("Pointer", false) ? Pointer(control, parts) : nlohmann::json(nullptr)},
            {"ActivePart", _lastActive}, {"Entities", entities}};
        std::ofstream output(R"(__STATE__)");
        output << result.dump();
    }
public:
    void OnStart() override
    {
        rei::GetInternalWorld()->AddSystem([this]
        {
            ECS_WORLD(rei::GetInternalWorld());
            const auto entity = FILTER(rei::editor::TransformationControl)->First();
            if (IS_DEAD(entity)) return;
            const auto& control = GET(entity, rei::editor::TransformationControl);
            const auto parts = Parts(control);
            for (const auto& part : parts) if (part.Active) _lastActive = part.Name;
            if (control.RectTransformBodyDragPending || control.RectTransformBodyDragActive) _lastActive = "body";
            std::ifstream file(R"(__COMMAND__)");
            if (!file) return;
            const auto command = nlohmann::json::parse(file, nullptr, false);
            if (command.is_discarded() || command.value("Step", 0) <= _step) return;
            _step = command.at("Step");
            const std::string action = command.value("Action", "read");
            if (action == "configure") Configure(command);
            if (action == "arm") _lastActive.clear();
            Publish(command, control, parts);
        });
    }
};
std::shared_ptr<rei::App> CreateApp() { return std::make_shared<TestApplication>(); }
