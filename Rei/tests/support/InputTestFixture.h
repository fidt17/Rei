#pragma once
#include "NativeTestSupport.h"
#include "Api/EditorEventsRelay.h"
#include "Engine/Services.h"
#include "Modules/Input/Input.h"
#include "GLFW/glfw3.h"

namespace rei::tests
{
    // Exact production callbacks, obtained through GLFW's public callback API.
    // Hidden windows have no GL context; every fixture lives in an owned child.
    class InputFixture
    {
    public:
        GLFWwindow* First = nullptr;
        GLFWwindow* Second = nullptr;
        GLFWkeyfun Key = nullptr;
        GLFWmousebuttonfun Mouse = nullptr;
        GLFWcursorposfun Cursor = nullptr;
        GLFWscrollfun Scroll = nullptr;
        std::shared_ptr<api::EditorEventsRelay> Relay = std::make_shared<api::EditorEventsRelay>();
        std::vector<api::EditorInputEvent> Events;

        InputFixture()
        {
            if (!IsIsolatedChild()) throw std::logic_error("InputFixture requires isolated child");
            REQUIRE(glfwInit() == GLFW_TRUE);
            glfwWindowHint(GLFW_VISIBLE, GLFW_FALSE);
            glfwWindowHint(GLFW_CLIENT_API, GLFW_NO_API);
            First = glfwCreateWindow(64, 48, "Rei native input first", nullptr, nullptr);
            Second = glfwCreateWindow(80, 60, "Rei native input second", nullptr, nullptr);
            REQUIRE(First != nullptr);
            REQUIRE(Second != nullptr);
            Services::GetInstance()->SetEditorEventsRelay(Relay);
            Relay->EditorInputReceivedEvent.append([this](const auto& event) { Events.push_back(event); });
            Select(First);
        }

        ~InputFixture()
        {
            glfwDestroyWindow(Second);
            glfwDestroyWindow(First);
            glfwTerminate();
            Services::GetInstance()->SetEditorEventsRelay(nullptr);
        }

        void Select(GLFWwindow* window)
        {
            Input::SetSource(window);
            Key = glfwSetKeyCallback(window, nullptr);
            Mouse = glfwSetMouseButtonCallback(window, nullptr);
            Cursor = glfwSetCursorPosCallback(window, nullptr);
            Scroll = glfwSetScrollCallback(window, nullptr);
            REQUIRE(Key != nullptr);
            REQUIRE(Mouse != nullptr);
            REQUIRE(Cursor != nullptr);
            REQUIRE(Scroll != nullptr);
            glfwSetKeyCallback(window, Key);
            glfwSetMouseButtonCallback(window, Mouse);
            glfwSetCursorPosCallback(window, Cursor);
            glfwSetScrollCallback(window, Scroll);
        }
    };
}
