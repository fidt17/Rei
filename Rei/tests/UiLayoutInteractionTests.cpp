#include "pch.h"
#include "glad/glad.h"
#include "support/BehaviourTestFixture.h"
#include "support/InputTestFixture.h"
#include "Common/Transform/RectTransformUtility.h"
#include "Modules/Render/UI/UIUtility.h"
#include "Modules/Input/Pointer/UIPointerCollisionSystem.h"
#include "Modules/Physics/PointerCollisionListener.h"
#include "Modules/Components/ActiveTag.h"
#include "Modules/Assets/Core/AssetPostLoadHandler.h"
#include "rei_behaviours/ui/Canvas.h"
#include "rei_behaviours/ui/RectTransform.h"
#include "rei_behaviours/ui/Button.h"
#include "rei_behaviours/ui/Image.h"

using namespace rei;
using namespace rei::tests;
using namespace rei::ui;
using nlohmann::json;

namespace
{
    json VectorField(const std::string& name, const f32 x, const f32 y)
    {
        return {{name, {{"Value", {{"x", {{"Value", x}}}, {"y", {{"Value", y}}}}}}}};
    }

    void CheckRect(const math::Rect& rect, const f32 minX, const f32 minY, const f32 maxX, const f32 maxY)
    {
        CHECK(rect.Min.x == Catch::Approx(minX).margin(1e-4f));
        CHECK(rect.Min.y == Catch::Approx(minY).margin(1e-4f));
        CHECK(rect.Max.x == Catch::Approx(maxX).margin(1e-4f));
        CHECK(rect.Max.y == Catch::Approx(maxY).margin(1e-4f));
    }

    RectTransform& AddRect(BehaviourFixture& fixture, const ecs::Entity entity)
    {
        auto& rect = fixture.Registry->Get<RectTransform>(entity);
        rect = RectTransform(7303, entity);
        return rect;
    }
}

TEST_CASE("Canvas scale blends width and height and clamps match outside range", "[native][coverage][coverage-remaining][ui-layout]")
{
    Canvas canvas;
    canvas.REI_SET(VectorField("_referenceResolution", 100, 100));
    for (const auto [match, expected] : {std::pair{-1.0f, 2.0f}, std::pair{0.0f, 2.0f}, std::pair{0.5f, 1.5f}, std::pair{1.0f, 1.0f}, std::pair{2.0f, 1.0f}})
    {
        canvas.REI_SET({{"_matchWidthOrHeight", {{"Value", match}}}});
        CHECK(ui_utility::CalculateCanvasScaleFactor(canvas, 200, 100) == Catch::Approx(expected));
        const auto rect = ui_utility::GetCanvasRect(canvas, 200, 100);
        CHECK(rect.Max.x * expected == Catch::Approx(200));
        CHECK(rect.Max.y * expected == Catch::Approx(100));
    }
}

TEST_CASE("Canvas constant pixel mode ignores reference size and match", "[native][coverage][coverage-remaining][ui-layout]")
{
    Canvas canvas;
    canvas.REI_SET({{"_scaleMode", {{"Value", static_cast<i32>(ConstantPixelSize)}}}, {"_matchWidthOrHeight", {{"Value", 0.5f}}}});
    canvas.REI_SET(VectorField("_referenceResolution", 100, 100));
    CHECK(ui_utility::CalculateCanvasScaleFactor(canvas, 200, 100) == 1);
    CheckRect(ui_utility::GetCanvasRect(canvas, 200, 100), 0, 0, 200, 100);
}

TEST_CASE("Canvas invalid reference and zero output retain finite positive scale", "[native][coverage][coverage-remaining][ui-layout]")
{
    Canvas canvas;
    canvas.REI_SET(VectorField("_referenceResolution", 0, -10));
    CHECK(ui_utility::CalculateCanvasScaleFactor(canvas, 200, 100) == 200);
    const auto scale = ui_utility::CalculateCanvasScaleFactor(canvas, 0, 0);
    CHECK(std::isfinite(scale));
    CHECK(scale > 0);
    CheckRect(ui_utility::GetCanvasRect(canvas, 0, 0), 0, 0, 0, 0);
}

TEST_CASE("UI rect stretches anchors with independent pivot and offset", "[native][coverage][coverage-remaining][ui-layout][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto root = fixture.Entity(100);
        const auto child = fixture.Entity(101);
        fixture.Registry->Get<Canvas>(root) = Canvas(7302, root);
        fixture.Registry->Get<Canvas>(root).REI_SET({{"_scaleMode", {{"Value", static_cast<i32>(ConstantPixelSize)}}}});
        auto& rect = AddRect(fixture, child);
        rect.REI_SET(VectorField("_anchorMin", 0, 0));
        rect.REI_SET(VectorField("_anchorMax", 1, 1));
        rect.REI_SET(VectorField("_pivot", 0, 1));
        rect.GetAnchoredPosition() = {10, -20};
        rect.GetSizeDelta() = {-20, -40};
        fixture.Registry->Get<Transform>(child).SetParent(root);
        CheckRect(ui_utility::CalculateRect(child, root, 200, 100), 10, 20, 190, 80);
        fixture.Registry->Del<Canvas>(root);
        fixture.Registry->Del<RectTransform>(child);
    });
}

TEST_CASE("UI nested rect inherits parent dimensions and anchored offset", "[native][coverage][coverage-remaining][ui-layout][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto root = fixture.Entity(100);
        const auto panel = fixture.Entity(101);
        const auto child = fixture.Entity(102);
        fixture.Registry->Get<Canvas>(root) = Canvas(7302, root);
        fixture.Registry->Get<Canvas>(root).REI_SET({{"_scaleMode", {{"Value", static_cast<i32>(ConstantPixelSize)}}}});
        auto& panelRect = AddRect(fixture, panel);
        panelRect.GetSizeDelta() = {100, 60};
        panelRect.GetAnchoredPosition() = {10, 5};
        auto& childRect = AddRect(fixture, child);
        childRect.GetSizeDelta() = {20, 10};
        childRect.GetAnchoredPosition() = {-10, 5};
        fixture.Registry->Get<Transform>(panel).SetParent(root);
        fixture.Registry->Get<Transform>(child).SetParent(panel);
        CheckRect(ui_utility::CalculateRect(panel, root, 200, 100), 60, 25, 160, 85);
        CheckRect(ui_utility::CalculateRect(child, root, 200, 100), 90, 55, 110, 65);
        fixture.Registry->Del<Canvas>(root);
        fixture.Registry->Del<RectTransform>(panel);
        fixture.Registry->Del<RectTransform>(child);
    });
}

TEST_CASE("UI canvas search follows nearest parent and reparenting", "[native][coverage][coverage-remaining][ui-layout][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto first = fixture.Entity(100);
        const auto second = fixture.Entity(101);
        const auto child = fixture.Entity(102);
        fixture.Registry->Get<Canvas>(first) = Canvas(7302, first);
        fixture.Registry->Get<Canvas>(second) = Canvas(7302, second);
        auto& transform = fixture.Registry->Get<Transform>(child);
        transform.SetParent(first);
        CHECK(ui_utility::FindCanvasEntity(child) == first);
        transform.SetParent(second);
        CHECK(ui_utility::FindCanvasEntity(child) == second);
        CHECK(ui_utility::FindCanvasEntity(second) == second);
        transform.SetParent(ecs::NULL_ENTITY);
        CHECK(ui_utility::FindCanvasEntity(child) == ecs::NULL_ENTITY);
        fixture.Registry->Del<Canvas>(first);
        fixture.Registry->Del<Canvas>(second);
    });
}

TEST_CASE("UI rotated scaled rect hit testing follows transformed quad", "[native][coverage][coverage-remaining][ui-layout][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto entity = fixture.Entity();
        auto& transform = fixture.Registry->Get<Transform>(entity);
        transform.GetLocalScale() = {2, 0.5f, 1};
        transform.SetRotation(math::Vector3(0, 0, 90));
        auto& rectTransform = AddRect(fixture, entity);
        const math::Rect rect{{0, 0}, {100, 20}};
        // Rotation around center (50,10): resulting half extents (5,100).
        CHECK(ui_utility::IsScreenPointInside({50, 100}, rect, rectTransform, transform));
        CHECK_FALSE(ui_utility::IsScreenPointInside({60, 10}, rect, rectTransform, transform));
        CHECK_FALSE(ui_utility::IsScreenPointInside({50, 111}, rect, rectTransform, transform));
        fixture.Registry->Del<RectTransform>(entity);
    });
}

TEST_CASE("UI pivot rotation keeps pivot fixed and collapses zero scale without hit", "[native][coverage][coverage-remaining][ui-layout][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto entity = fixture.Entity();
        auto& transform = fixture.Registry->Get<Transform>(entity);
        transform.SetRotation(math::Vector3(0, 0, 90));
        auto& rectTransform = AddRect(fixture, entity);
        rectTransform.REI_SET(VectorField("_pivot", 0, 0));
        const math::Rect rect{{10, 20}, {110, 40}};
        const auto model = ui_utility::BuildModelMatrix(rect, rectTransform, transform);
        const auto pivot = model * glm::vec4(-0.5f, -0.5f, 0, 1);
        CHECK(pivot.x == Catch::Approx(10).margin(1e-4f));
        CHECK(pivot.y == Catch::Approx(20).margin(1e-4f));
        transform.GetLocalScale() = {0, 1, 1};
        CHECK_FALSE(ui_utility::IsScreenPointInside({10, 20}, rect, rectTransform, transform));
        fixture.Registry->Del<RectTransform>(entity);
    });
}

TEST_CASE("UI rectangle edges are inclusive and exterior points miss", "[native][coverage][coverage-remaining][ui-layout]")
{
    const math::Rect rect{{10, 20}, {30, 40}};
    CHECK(render::ui_render_utility::IsPointInsideRect({10, 20}, rect));
    CHECK(render::ui_render_utility::IsPointInsideRect({30, 40}, rect));
    CHECK_FALSE(render::ui_render_utility::IsPointInsideRect({9.99f, 20}, rect));
    CHECK_FALSE(render::ui_render_utility::IsPointInsideRect({30, 40.01f}, rect));
}

TEST_CASE("UI aspect preservation fits loaded texture dimensions without GPU upload", "[native][coverage][coverage-remaining][ui-layout][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto entity = fixture.Entity();
        assets::AssetPostLoadHandler::ScopedPostLoadSuppression suppress(true);
        // Exercise actual asset binding and metadata. Deferred GPU phase stays unflushed.
        const auto wide = fixture.Assets->CreateAssetWithId<render::Texture>("wide", 2, 1, GL_RGB, std::vector<u8>(6, 255));
        const auto tall = fixture.Assets->CreateAssetWithId<render::Texture>("tall", 1, 2, GL_RGB, std::vector<u8>(6, 255));
        Image image(7304, entity);
        image.REI_SET({{"_preserveAspect", {{"Value", true}}}, {"_texture", {{"Value", {{"Id", {{"Value", "wide"}}}}}}}});
        REQUIRE(image.GetTexture().IsLoaded());
        CHECK(image.GetTexture()->GetId() == 0);
        CheckRect(ui_utility::ApplyAspectPreservation({{0, 0}, {100, 100}}, image), 0, 25, 100, 75);
        image.REI_SET({{"_texture", {{"Value", {{"Id", {{"Value", "tall"}}}}}}}});
        CheckRect(ui_utility::ApplyAspectPreservation({{0, 0}, {100, 100}}, image), 25, 0, 75, 100);
        image.REI_SET({{"_preserveAspect", {{"Value", false}}}});
        CheckRect(ui_utility::ApplyAspectPreservation({{0, 0}, {100, 100}}, image), 0, 0, 100, 100);
    });
}

TEST_CASE("UI aspect preservation retains rect with unloaded texture or nonpositive extent", "[native][coverage][coverage-remaining][ui-layout]")
{
    Image image;
    image.REI_SET({{"_preserveAspect", {{"Value", true}}}});
    CheckRect(ui_utility::ApplyAspectPreservation({{10, 20}, {30, 40}}, image), 10, 20, 30, 40);
    CheckRect(ui_utility::ApplyAspectPreservation({{10, 20}, {10, 40}}, image), 10, 20, 10, 40);
}

TEST_CASE("UI hierarchy ordering gives descendants precedence and follows reparenting", "[native][coverage][coverage-remaining][ui-layout][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto root = fixture.Entity(100);
        const auto panel = fixture.Entity(101);
        const auto child = fixture.Entity(102);
        fixture.Registry->Get<Transform>(panel).SetParent(root, 0);
        fixture.Registry->Get<Transform>(child).SetParent(panel, 0);
        CHECK(render::ui_render_utility::BuildHierarchySortKey(root) == std::vector<i32>{0});
        CHECK(render::ui_render_utility::BuildHierarchySortKey(child) == std::vector<i32>{0, 0, 0});
        CHECK(render::ui_render_utility::IsHigherUiEntity(child, panel));
        CHECK_FALSE(render::ui_render_utility::IsHigherUiEntity(panel, child));
        CHECK(render::ui_render_utility::IsHigherUiEntity(child, ecs::NULL_ENTITY));
        fixture.Registry->Get<Transform>(child).SetParent(root, 1);
        CHECK(render::ui_render_utility::BuildHierarchySortKey(child) == std::vector<i32>{0, 1});
        CHECK(render::ui_render_utility::IsHigherUiEntity(child, panel));
    });
}

TEST_CASE("UI button bubbling finds nearest live parent and stops at root", "[native][coverage][coverage-remaining][ui-layout][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto root = fixture.Entity(100);
        const auto panel = fixture.Entity(101);
        const auto child = fixture.Entity(102);
        fixture.Registry->Get<Transform>(panel).SetParent(root);
        fixture.Registry->Get<Transform>(child).SetParent(panel);
        fixture.Registry->Get<Button>(root) = Button(7310, root);
        fixture.Registry->Get<Button>(panel) = Button(7310, panel);
        CHECK(render::ui_render_utility::FindNearestButtonEntity(child) == panel);
        CHECK(render::ui_render_utility::FindNearestButtonEntity(panel) == panel);
        fixture.Registry->Del<Button>(panel);
        CHECK(render::ui_render_utility::FindNearestButtonEntity(child) == root);
        fixture.Registry->Del<Button>(root);
        CHECK(render::ui_render_utility::FindNearestButtonEntity(child) == ecs::NULL_ENTITY);
    });
}

TEST_CASE("UI pointer state exits when no active camera remains", "[native][coverage][coverage-remaining][ui-pointer][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto entity = fixture.Entity();
        AddRect(fixture, entity);
        fixture.Registry->Get<ActiveTag>(entity);
        auto& listener = fixture.Registry->Get<physics::PointerCollisionListener>(entity);
        listener.IsInside = true;
        listener.DidEnter = true;
        fixture.World->RefreshAll();
        input::UIPointerCollisionSystem system(fixture.World);
        system.OnUpdate();
        CHECK_FALSE(listener.IsInside);
        CHECK_FALSE(listener.DidEnter);
        CHECK(listener.DidExit);
        fixture.Registry->Del<RectTransform>(entity);
    });
}

TEST_CASE("Button enters presses releases and clicks once while pointer stays inside", "[native][coverage][coverage-remaining][ui-button][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        InputFixture input;
        const auto entity = fixture.Entity();
        auto& button = fixture.Registry->Get<Button>(entity);
        button = Button(7310, entity);
        std::vector<std::string> events;
        button.PointerEnteredEvent.append([&] { events.push_back("enter"); });
        button.PressedEvent.append([&] { events.push_back("press"); });
        button.ReleasedEvent.append([&] { events.push_back("release"); });
        button.ClickedEvent.append([&] { events.push_back("click"); });
        auto& listener = fixture.Registry->Get<physics::PointerCollisionListener>(entity);
        listener.IsInside = true;
        button.Update();
        input.Mouse(input.First, GLFW_MOUSE_BUTTON_LEFT, GLFW_PRESS, 0);
        button.Update();
        CHECK(button.IsPressed());
        Input::Update();
        button.Update();
        input.Mouse(input.First, GLFW_MOUSE_BUTTON_LEFT, GLFW_RELEASE, 0);
        button.Update();
        CHECK_FALSE(button.IsPressed());
        button.Update();
        CHECK(events == std::vector<std::string>{"enter", "press", "release", "click"});
        fixture.Registry->Del<Button>(entity);
    });
}

TEST_CASE("Button release outside emits release without click", "[native][coverage][coverage-remaining][ui-button][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        InputFixture input;
        const auto entity = fixture.Entity();
        auto& button = fixture.Registry->Get<Button>(entity);
        button = Button(7310, entity);
        i32 clicked = 0, released = 0, exited = 0;
        button.ClickedEvent.append([&] { ++clicked; });
        button.ReleasedEvent.append([&] { ++released; });
        button.PointerExitedEvent.append([&] { ++exited; });
        auto& listener = fixture.Registry->Get<physics::PointerCollisionListener>(entity);
        listener.IsInside = true;
        input.Mouse(input.First, GLFW_MOUSE_BUTTON_LEFT, GLFW_PRESS, 0);
        button.Update();
        Input::Update();
        listener.IsInside = false;
        button.Update();
        input.Mouse(input.First, GLFW_MOUSE_BUTTON_LEFT, GLFW_RELEASE, 0);
        button.Update();
        CHECK_FALSE(button.IsPointerInside());
        CHECK_FALSE(button.IsPressed());
        CHECK(clicked == 0);
        CHECK(released == 1);
        CHECK(exited == 1);
        fixture.Registry->Del<Button>(entity);
    });
}

TEST_CASE("Button becoming noninteractable during press cancels later click", "[native][coverage][coverage-remaining][ui-button][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        InputFixture input;
        const auto entity = fixture.Entity();
        auto& button = fixture.Registry->Get<Button>(entity);
        button = Button(7310, entity);
        i32 clicked = 0;
        button.ClickedEvent.append([&] { ++clicked; });
        fixture.Registry->Get<physics::PointerCollisionListener>(entity).IsInside = true;
        input.Mouse(input.First, GLFW_MOUSE_BUTTON_LEFT, GLFW_PRESS, 0);
        button.Update();
        CHECK(button.IsPressed());
        button.SetInteractable(false);
        CHECK_FALSE(button.IsPressed());
        CHECK_FALSE(button.IsPointerInside());
        Input::Update();
        input.Mouse(input.First, GLFW_MOUSE_BUTTON_LEFT, GLFW_RELEASE, 0);
        button.Update();
        button.SetInteractable(true);
        button.Update();
        CHECK(clicked == 0);
        fixture.Registry->Del<Button>(entity);
    });
}
