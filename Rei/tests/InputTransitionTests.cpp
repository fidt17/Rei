#include "pch.h"
#include "support/InputTestFixture.h"

using namespace rei;
using namespace rei::tests;

TEST_CASE("Input key press hold repeat release follows frame boundaries", "[native][coverage][coverage-remaining][input][isolated]")
{
    Isolated([]
    {
        InputFixture fixture;
        CHECK(Input::IsKeyUp(GLFW_KEY_A));
        fixture.Key(fixture.First, GLFW_KEY_A, 0, GLFW_PRESS, 0);
        CHECK(Input::IsKeyDown(GLFW_KEY_A));
        CHECK(Input::IsKeyPressed(GLFW_KEY_A));
        Input::Update();
        CHECK(Input::IsKeyDown(GLFW_KEY_A));
        CHECK_FALSE(Input::IsKeyPressed(GLFW_KEY_A));
        fixture.Key(fixture.First, GLFW_KEY_A, 0, GLFW_REPEAT, 0);
        CHECK(Input::IsKeyDown(GLFW_KEY_A));
        CHECK_FALSE(Input::IsKeyPressed(GLFW_KEY_A));
        CHECK(fixture.Events.size() == 1);
        fixture.Key(fixture.First, GLFW_KEY_A, 0, GLFW_RELEASE, 0);
        CHECK(Input::IsKeyUp(GLFW_KEY_A));
        CHECK(Input::IsKeyReleased(GLFW_KEY_A));
        Input::Update();
        CHECK_FALSE(Input::IsKeyReleased(GLFW_KEY_A));
    });
}

TEST_CASE("Input mouse press hold release preserves other buttons", "[native][coverage][coverage-remaining][input][isolated]")
{
    Isolated([]
    {
        InputFixture fixture;
        fixture.Mouse(fixture.First, GLFW_MOUSE_BUTTON_LEFT, GLFW_PRESS, 0);
        CHECK(Input::IsMouseButtonPressed(GLFW_MOUSE_BUTTON_LEFT));
        CHECK(Input::IsMouseButtonUp(GLFW_MOUSE_BUTTON_RIGHT));
        Input::Update();
        fixture.Mouse(fixture.First, GLFW_MOUSE_BUTTON_RIGHT, GLFW_PRESS, 0);
        CHECK_FALSE(Input::IsMouseButtonPressed(GLFW_MOUSE_BUTTON_LEFT));
        CHECK(Input::IsMouseButtonDown(GLFW_MOUSE_BUTTON_LEFT));
        CHECK(Input::IsMouseButtonPressed(GLFW_MOUSE_BUTTON_RIGHT));
        fixture.Mouse(fixture.First, GLFW_MOUSE_BUTTON_LEFT, GLFW_RELEASE, 0);
        CHECK(Input::IsMouseButtonReleased(GLFW_MOUSE_BUTTON_LEFT));
        CHECK(Input::IsMouseButtonDown(GLFW_MOUSE_BUTTON_RIGHT));
    });
}

TEST_CASE("Input accepts final GLFW keyboard code MENU", "[native][coverage][coverage-remaining][input][isolated]")
{
    Isolated([]
    {
        InputFixture fixture;
        fixture.Key(fixture.First, GLFW_KEY_MENU, 0, GLFW_PRESS, 0);
        CHECK(Input::IsKeyDown(GLFW_KEY_MENU));
        CHECK(Input::IsKeyPressed(GLFW_KEY_MENU));
        CHECK(fixture.Events.size() == 1);
        Input::Update();
        fixture.Key(fixture.First, GLFW_KEY_MENU, 0, GLFW_RELEASE, 0);
        CHECK(Input::IsKeyReleased(GLFW_KEY_MENU));
    });
}

TEST_CASE("Input accepts eighth GLFW mouse button", "[native][coverage][coverage-remaining][input][isolated]")
{
    Isolated([]
    {
        InputFixture fixture;
        fixture.Mouse(fixture.First, GLFW_MOUSE_BUTTON_8, GLFW_PRESS, 0);
        CHECK(Input::IsMouseButtonDown(GLFW_MOUSE_BUTTON_8));
        CHECK(Input::IsMouseButtonPressed(GLFW_MOUSE_BUTTON_8));
        CHECK(fixture.Events.size() == 1);
        Input::Update();
        fixture.Mouse(fixture.First, GLFW_MOUSE_BUTTON_8, GLFW_RELEASE, 0);
        CHECK(Input::IsMouseButtonReleased(GLFW_MOUSE_BUTTON_8));
    });
}

TEST_CASE("Input invalid codes neither mutate state nor emit editor events", "[native][coverage][coverage-remaining][input][isolated]")
{
    Isolated([]
    {
        InputFixture fixture;
        for (const i32 code : {-1, GLFW_KEY_LAST + 1, 100000})
        {
            fixture.Key(fixture.First, code, 0, GLFW_PRESS, 0);
            CHECK_FALSE(Input::IsKeyDown(code));
            CHECK_FALSE(Input::IsKeyUp(code));
            CHECK_FALSE(Input::IsKeyPressed(code));
            CHECK_FALSE(Input::IsKeyReleased(code));
        }
        for (const i32 code : {-1, GLFW_MOUSE_BUTTON_LAST + 1, 100000})
        {
            fixture.Mouse(fixture.First, code, GLFW_PRESS, 0);
            CHECK_FALSE(Input::IsMouseButtonDown(code));
            CHECK_FALSE(Input::IsMouseButtonUp(code));
            CHECK_FALSE(Input::IsMouseButtonPressed(code));
            CHECK_FALSE(Input::IsMouseButtonReleased(code));
        }
        CHECK(fixture.Events.empty());
    });
}

TEST_CASE("Input editor relay retains native codes modifiers and cursor coordinates", "[native][coverage][coverage-remaining][input][isolated]")
{
    Isolated([]
    {
        InputFixture fixture;
        fixture.Cursor(fixture.First, 12.5, 27.25);
        fixture.Key(fixture.First, GLFW_KEY_F12, 99, GLFW_PRESS, GLFW_MOD_CONTROL | GLFW_MOD_SHIFT);
        fixture.Mouse(fixture.First, GLFW_MOUSE_BUTTON_RIGHT, GLFW_RELEASE, GLFW_MOD_ALT);
        REQUIRE(fixture.Events.size() == 2);
        CHECK(fixture.Events[0].Type == api::KeyDown);
        CHECK(fixture.Events[0].Code == GLFW_KEY_F12);
        CHECK(fixture.Events[0].Mods == (GLFW_MOD_CONTROL | GLFW_MOD_SHIFT));
        CHECK(fixture.Events[0].MouseX == 12.5f);
        CHECK(fixture.Events[0].MouseY == 27.25f);
        CHECK(fixture.Events[1].Type == api::MouseButtonUp);
        CHECK(fixture.Events[1].Mods == GLFW_MOD_ALT);
    });
}

TEST_CASE("Input source replacement resets held keys and buttons", "[native][coverage][coverage-remaining][input][isolated]")
{
    Isolated([]
    {
        InputFixture fixture;
        fixture.Key(fixture.First, GLFW_KEY_A, 0, GLFW_PRESS, 0);
        fixture.Mouse(fixture.First, GLFW_MOUSE_BUTTON_LEFT, GLFW_PRESS, 0);
        Input::Update();
        fixture.Select(fixture.Second);
        CHECK(Input::IsKeyUp(GLFW_KEY_A));
        CHECK_FALSE(Input::IsKeyReleased(GLFW_KEY_A));
        CHECK(Input::IsMouseButtonUp(GLFW_MOUSE_BUTTON_LEFT));
        CHECK_FALSE(Input::IsMouseButtonReleased(GLFW_MOUSE_BUTTON_LEFT));
    });
}

TEST_CASE("Input source replacement disconnects callbacks from old window", "[native][coverage][coverage-remaining][input][isolated]")
{
    Isolated([]
    {
        InputFixture fixture;
        const auto oldKey = fixture.Key;
        fixture.Select(fixture.Second);
        CHECK(glfwSetKeyCallback(fixture.First, nullptr) == nullptr);
        CHECK(glfwSetMouseButtonCallback(fixture.First, nullptr) == nullptr);
        CHECK(glfwSetCursorPosCallback(fixture.First, nullptr) == nullptr);
        CHECK(glfwSetScrollCallback(fixture.First, nullptr) == nullptr);
        // A callback already queued by GLFW must also reject its old source.
        oldKey(fixture.First, GLFW_KEY_B, 0, GLFW_PRESS, 0);
        CHECK_FALSE(Input::IsKeyDown(GLFW_KEY_B));
        CHECK(fixture.Events.empty());
    });
}

TEST_CASE("Input scroll records latest callback and resets at frame boundary", "[native][coverage][coverage-remaining][input][isolated]")
{
    Isolated([]
    {
        InputFixture fixture;
        fixture.Scroll(fixture.First, 1.25, -2.5);
        CHECK(Input::GetScrollX() == 1.25);
        CHECK(Input::GetScrollY() == -2.5);
        fixture.Scroll(fixture.First, -0.5, 3);
        // Characterizes current last-event policy; accumulation remains a product decision.
        CHECK(Input::GetScrollX() == -0.5);
        CHECK(Input::GetScrollY() == 3);
        Input::Update();
        CHECK(Input::GetScrollX() == 0);
        CHECK(Input::GetScrollY() == 0);
    });
}
