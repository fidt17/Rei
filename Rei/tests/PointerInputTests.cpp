#include "pch.h"
#include "support/BehaviourTestFixture.h"
#include "support/InputTestFixture.h"
#include "support/GeometryTestSupport.h"
#include "Modules/Input/Pointer/PointerCollisionSystem.h"
#include "Modules/Physics/PointerCollisionListener.h"
#include "Modules/Physics/SphereCollider.h"
#include "Modules/Components/ActiveTag.h"
#include "rei_behaviours/render/camera/Camera.h"

using namespace rei;
using namespace rei::tests;

namespace
{
    struct PointerFixture
    {
        BehaviourFixture Native;
        InputFixture Input;
        ecs::Entity CameraEntity = Native.Entity(100);
        ecs::Entity Target = Native.Entity(101);
        input::PointerCollisionSystem System{Native.World};

        PointerFixture()
        {
            auto& camera = Native.Registry->Get<render::Camera>(CameraEntity);
            camera = render::Camera(7301, CameraEntity);
            camera.SetOutputSize(256, 256);
            Native.Registry->Get<Transform>(Target).GetLocalPosition() = {0, 0, 5};
            Native.Registry->Get<ActiveTag>(Target);
            Native.Registry->Get<physics::PointerCollisionListener>(Target).Collider = std::make_shared<physics::SphereCollider>();
            Native.World->RefreshAll();
            Input.Cursor(Input.First, 128, 128);
        }

        ~PointerFixture() { Native.Registry->Del<render::Camera>(CameraEntity); }

        physics::PointerCollisionListener& Listener() { return Native.Registry->Get<physics::PointerCollisionListener>(Target); }
    };
}

TEST_CASE("Pointer input camera ray transform and collider produce enter stay exit", "[native][coverage][coverage-remaining][physics-pointer][isolated]")
{
    Isolated([]
    {
        PointerFixture fixture;
        fixture.System.OnUpdate();
        const auto& listener = fixture.Listener();
        CHECK(listener.IsInside);
        CHECK(listener.DidEnter);
        CHECK_FALSE(listener.DidExit);
        CheckVector(listener.CollisionPoint, {0, 0, 4});
        fixture.System.OnUpdate();
        CHECK(listener.IsInside);
        CHECK_FALSE(listener.DidEnter);
        CHECK_FALSE(listener.DidExit);
        fixture.Input.Cursor(fixture.Input.First, 0, 0);
        fixture.System.OnUpdate();
        CHECK_FALSE(listener.IsInside);
        CHECK_FALSE(listener.DidEnter);
        CHECK(listener.DidExit);
        fixture.System.OnUpdate();
        CHECK_FALSE(listener.DidExit);
    });
}

TEST_CASE("Pointer removing collider exits previous hit", "[native][coverage][coverage-remaining][physics-pointer][isolated]")
{
    Isolated([]
    {
        PointerFixture fixture;
        fixture.System.OnUpdate();
        REQUIRE(fixture.Listener().IsInside);
        fixture.Listener().Collider.reset();
        fixture.System.OnUpdate();
        CHECK_FALSE(fixture.Listener().IsInside);
        CHECK(fixture.Listener().DidExit);
        CHECK_FALSE(fixture.Listener().DidEnter);
    });
}

TEST_CASE("Pointer inactive entity loses previous hit", "[native][coverage][coverage-remaining][physics-pointer][isolated]")
{
    Isolated([]
    {
        PointerFixture fixture;
        fixture.System.OnUpdate();
        REQUIRE(fixture.Listener().IsInside);
        fixture.Native.Registry->Del<ActiveTag>(fixture.Target);
        fixture.Native.World->RefreshAll();
        fixture.System.OnUpdate();
        CHECK_FALSE(fixture.Listener().IsInside);
        CHECK_FALSE(fixture.Listener().DidEnter);
    });
}

TEST_CASE("Pointer camera removal clears previous hit state", "[native][coverage][coverage-remaining][physics-pointer][isolated]")
{
    Isolated([]
    {
        PointerFixture fixture;
        fixture.System.OnUpdate();
        REQUIRE(fixture.Listener().IsInside);
        fixture.Native.Registry->Del<render::Camera>(fixture.CameraEntity);
        fixture.Native.World->RefreshAll();
        fixture.System.OnUpdate();
        CHECK_FALSE(fixture.Listener().IsInside);
        CHECK_FALSE(fixture.Listener().DidEnter);
        CHECK(fixture.Listener().DidExit);
    });
}

TEST_CASE("Pointer transform removal emits one exit and keeps last collision point", "[native][physics-pointer][isolated]")
{
    Isolated([]
    {
        PointerFixture fixture;
        fixture.System.OnUpdate();
        REQUIRE(fixture.Listener().IsInside);
        const auto lastPoint = fixture.Listener().CollisionPoint;
        fixture.Native.Registry->Del<Transform>(fixture.Target);
        fixture.Native.World->Refresh();
        fixture.System.OnUpdate();
        CHECK_FALSE(fixture.Listener().IsInside);
        CHECK(fixture.Listener().DidExit);
        CheckVector(fixture.Listener().CollisionPoint, lastPoint);
        fixture.System.OnUpdate();
        CHECK_FALSE(fixture.Listener().DidEnter);
        CHECK_FALSE(fixture.Listener().DidExit);
    });
}

TEST_CASE("Pointer inactive collider reactivation emits fresh enter", "[native][physics-pointer][isolated]")
{
    Isolated([]
    {
        PointerFixture fixture;
        fixture.System.OnUpdate();
        REQUIRE(fixture.Listener().IsInside);
        fixture.Native.Registry->Del<ActiveTag>(fixture.Target);
        fixture.Native.World->Refresh();
        fixture.System.OnUpdate();
        CHECK(fixture.Listener().DidExit);
        fixture.System.OnUpdate();
        CHECK_FALSE(fixture.Listener().DidExit);
        fixture.Native.Registry->Get<ActiveTag>(fixture.Target);
        fixture.Native.World->Refresh();
        fixture.System.OnUpdate();
        CHECK(fixture.Listener().IsInside);
        CHECK(fixture.Listener().DidEnter);
        CHECK_FALSE(fixture.Listener().DidExit);
    });
}
