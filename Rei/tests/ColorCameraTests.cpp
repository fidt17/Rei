#include "pch.h"
#include "support/BehaviourTestFixture.h"
#include "support/GeometryTestSupport.h"
#include "rei_behaviours/render/camera/Camera.h"
#include "Modules/Render/Color/Color.h"

using namespace rei;
using namespace rei::tests;
using namespace rei::render;

namespace
{
    void CheckColor(const Color& value, f32 r, f32 g, f32 b, f32 a)
    {
        CHECK(value.r == Catch::Approx(r).margin(1e-6f));
        CHECK(value.g == Catch::Approx(g).margin(1e-6f));
        CHECK(value.b == Catch::Approx(b).margin(1e-6f));
        CHECK(value.a == Catch::Approx(a).margin(1e-6f));
    }

    Camera& AddCamera(BehaviourFixture& fixture, ecs::Entity entity)
    {
        auto& camera = fixture.Registry->Get<Camera>(entity);
        camera = Camera(7301, entity);
        camera.SetOutputSize(256, 256);
        return camera;
    }

    bool Finite(const glm::mat4& matrix)
    {
        for (i32 col = 0; col < 4; ++col)
            for (i32 row = 0; row < 4; ++row)
                if (!std::isfinite(matrix[col][row])) return false;
        return true;
    }
}

TEST_CASE("Color parses shorthand mixed case RGB RGBA and optional hash", "[native][coverage][coverage-remaining][color]")
{
    CheckColor(Color::FromHex("#aB3"), 170 / 255.0f, 187 / 255.0f, 51 / 255.0f, 1);
    CheckColor(Color::FromHex("12ABef"), 18 / 255.0f, 171 / 255.0f, 239 / 255.0f, 1);
    CheckColor(Color::FromHex("#12abEF80"), 18 / 255.0f, 171 / 255.0f, 239 / 255.0f, 128 / 255.0f);
    CheckColor(Color::FromHex("00000000"), 0, 0, 0, 0);
    CheckColor(Color::FromHex("FFFFFF"), 1, 1, 1, 1);
}

TEST_CASE("Color rejects unsupported hex lengths", "[native][coverage][coverage-remaining][color]")
{
    for (const auto* text : {"", "#", "ab", "abcd", "12345", "1234567", "123456789"})
    {
        CAPTURE(text);
        CHECK_THROWS_AS(Color::FromHex(text), std::invalid_argument);
    }
}

TEST_CASE("Color rejects partially parsed hex bytes", "[native][coverage][coverage-remaining][color][isolated]")
{
    Isolated([]
    {
        common::logging::Log::Initialize();
        CHECK_THROWS_AS(Color::FromHex("1G2345"), std::invalid_argument);
        CHECK_THROWS_AS(Color::FromHex("12345Z"), std::invalid_argument);
    });
}

TEST_CASE("Color rejects invalid byte symbols before returning a value", "[native][coverage][coverage-remaining][color][isolated]")
{
    Isolated([]
    {
        common::logging::Log::Initialize();
        CHECK_THROWS_AS(Color::FromHex("GG0000"), std::invalid_argument);
        CHECK_THROWS_AS(Color::FromHex("#xyz"), std::invalid_argument);
    });
}

TEST_CASE("Color operations retain alpha and interpolate all channels", "[native][coverage][coverage-remaining][color]")
{
    CheckColor(Color::Lerp(Color(0.2f, 0.4f, 0.6f, 0.8f), Color(0.6f, 0.8f, 1, 0.4f), 0.25f), 0.3f, 0.5f, 0.7f, 0.7f);
    CheckColor(Color(0.2f, 0.4f, 0.6f, 0.8f) * Color(0.5f, 0.25f, 0, 0.5f), 0.1f, 0.1f, 0, 0.4f);
    CHECK(Color::Clear() == Color(0, 0, 0, 0));
    CHECK(Color::Black() == Color(0, 0, 0, 1));
    CHECK_FALSE(Color::Clear() == Color::Black());
}

TEST_CASE("Perspective camera maps center to forward ray and ray points back to pixels", "[native][coverage][coverage-remaining][camera-math][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto entity = fixture.Entity();
        auto& camera = AddCamera(fixture, entity);
        const auto center = camera.WorldToScreenPosition({0, 0, 10});
        CHECK(center.x == Catch::Approx(128));
        CHECK(center.y == Catch::Approx(128));
        CheckVector(camera.GetScreenPointToRay(128, 128).Origin, {0, 0, 0});
        CheckVector(camera.GetScreenPointToRay(128, 128).Direction, {0, 0, 1});
        for (const math::Vector2 pixel : {math::Vector2(32, 48), math::Vector2(224, 200)})
        {
            const auto ray = camera.GetScreenPointToRay(pixel.x, pixel.y);
            CHECK(ray.Direction.Length() == Catch::Approx(1).margin(1e-5f));
            const auto screen = camera.WorldToScreenPosition(ray.Origin + ray.Direction * 10.0f);
            CHECK(screen.x == Catch::Approx(pixel.x).margin(1e-3f));
            CHECK(screen.y == Catch::Approx(pixel.y).margin(1e-3f));
        }
        fixture.Registry->Del<Camera>(entity);
    });
}

TEST_CASE("Camera view and screen rays follow rotated translated parent", "[native][coverage][coverage-remaining][camera-math][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto parent = fixture.Entity(100);
        const auto entity = fixture.Entity(101);
        auto& parentTransform = fixture.Registry->Get<Transform>(parent);
        parentTransform.GetLocalPosition() = {10, 20, 30};
        parentTransform.SetRotation(math::Vector3(0, 90, 0));
        auto& transform = fixture.Registry->Get<Transform>(entity);
        transform.SetParent(parent);
        transform.GetLocalPosition() = {0, 0, 2};
        auto& camera = AddCamera(fixture, entity);
        const auto ray = camera.GetScreenPointToRay(128, 128);
        CheckVector(ray.Origin, {12, 20, 30});
        CheckVector(ray.Direction, {1, 0, 0});
        const auto screen = camera.WorldToScreenPosition({22, 20, 30});
        CHECK(screen.x == Catch::Approx(128).margin(1e-3f));
        CHECK(screen.y == Catch::Approx(128).margin(1e-3f));
        fixture.Registry->Del<Camera>(entity);
    });
}

TEST_CASE("Orthographic camera rays stay parallel and preserve screen position", "[native][coverage][coverage-remaining][camera-math][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto entity = fixture.Entity();
        auto& camera = AddCamera(fixture, entity);
        camera.SetPerspective(Orthographic);
        const auto left = camera.GetScreenPointToRay(0, 128);
        const auto right = camera.GetScreenPointToRay(256, 128);
        CheckVector(left.Direction, {0, 0, 1});
        CheckVector(right.Direction, left.Direction);
        CheckVector(left.Origin, {-4, 0, 0});
        CheckVector(right.Origin, {4, 0, 0});
        const auto top = camera.GetScreenPointToRay(128, 0);
        CheckVector(top.Origin, {0, 4, 0});
        const auto screen = camera.WorldToScreenPosition(top.Origin + top.Direction * 10.0f);
        CHECK(screen.x == Catch::Approx(128).margin(1e-3f));
        CHECK(screen.y == Catch::Approx(0).margin(1e-3f));
        fixture.Registry->Del<Camera>(entity);
    });
}

TEST_CASE("Camera sanitizes output dimensions FOV and inverted perspective clipping", "[native][coverage][coverage-remaining][camera-math][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto entity = fixture.Entity();
        auto& camera = AddCamera(fixture, entity);
        camera.SetOutputSize(0, -3);
        i32 width = 0, height = 0;
        camera.GetOutputSize(width, height);
        CHECK(width == 1);
        CHECK(height == 1);
        for (const f32 fov : {-100.0f, 0.0f, 180.0f, 500.0f})
        {
            camera.REI_SET({{"_fov", {{"Value", fov}}}, {"_nearClipPlane", {{"Value", 10}}}, {"_farClipPlane", {{"Value", 1}}}});
            CHECK(Finite(camera.GetProjectionMatrix()));
            const auto ray = camera.GetScreenPointToRay(0.5f, 0.5f);
            CheckVector(ray.Direction, {0, 0, 1});
        }
        fixture.Registry->Del<Camera>(entity);
    });
}

TEST_CASE("Camera orthographic projection honors configured clipping planes", "[native][coverage][coverage-remaining][camera-math][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto entity = fixture.Entity();
        auto& camera = AddCamera(fixture, entity);
        camera.SetPerspective(Orthographic);
        camera.REI_SET({{"_nearClipPlane", {{"Value", 2}}}, {"_farClipPlane", {{"Value", 12}}}});
        const auto projection = camera.GetProjectionMatrix();
        const auto nearClip = projection * camera.GetViewMatrix() * glm::vec4(0, 0, 2, 1);
        const auto farClip = projection * camera.GetViewMatrix() * glm::vec4(0, 0, 12, 1);
        CHECK(nearClip.z / nearClip.w == Catch::Approx(-1).margin(1e-4f));
        CHECK(farClip.z / farClip.w == Catch::Approx(1).margin(1e-4f));
        fixture.Registry->Del<Camera>(entity);
    });
}

TEST_CASE("Camera constant scale grows with perspective distance and stays fixed in orthographic", "[native][coverage][coverage-remaining][camera-math][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto entity = fixture.Entity();
        auto& camera = AddCamera(fixture, entity);
        const auto nearScale = camera.CalculateConstantScale({0, 0, 10}, 2);
        CHECK(nearScale > 0);
        CHECK(camera.CalculateConstantScale({0, 0, 20}, 2) == Catch::Approx(nearScale * 2));
        camera.SetPerspective(Orthographic);
        CHECK(camera.CalculateConstantScale({0, 0, 10}, 2) == Catch::Approx(camera.CalculateConstantScale({0, 0, 20}, 2)));
        fixture.Registry->Del<Camera>(entity);
    });
}

TEST_CASE("sRGB color decoding keeps authoring values and alpha unchanged", "[native][color][srgb]")
{
    const Color authoring(128.0f / 255.0f, 0.04045f, 1.0f, 0.37f);
    const auto linear = authoring.ToLinear();
    CheckColor(linear, 0.2158605f, 0.003130805f, 1.0f, 0.37f);
    CheckColor(authoring, 128.0f / 255.0f, 0.04045f, 1.0f, 0.37f);
    CheckColor(Color::Black().ToLinear(), 0, 0, 0, 1);
}

TEST_CASE("sRGB decoding handles near-black ramp and extended color range", "[native][color][srgb]")
{
    CheckColor(Color(0.02f, 0.04046f, 2.0f, 0).ToLinear(), 0.001547988f, 0.003131594f, 4.9538458f, 0);
    f32 previous = -1;
    for (i32 code = 0; code <= 255; ++code)
    {
        const auto value = Color(static_cast<f32>(code) / 255.0f, 0, 0).ToLinear().r;
        CHECK(value > previous);
        const auto encoded = value <= 0.0031308f ? 12.92f * value : 1.055f * std::pow(value, 1.0f / 2.4f) - 0.055f;
        CHECK(std::lround(encoded * 255.0f) == code);
        previous = value;
    }
}
