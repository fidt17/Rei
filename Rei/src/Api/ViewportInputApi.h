#pragma once
#include "Engine/Engine.h"
#include "Modules/Input/Input.h"

// Called off the engine thread while Editor holds its native lifecycle lease.
REI_EXTERN_API inline i32 SimulateEditorInput(const char* requestJson)
{
    if (!requestJson) return -3;
    auto& engine = rei::GetEngine();
    if (!engine.IsRunning() || !engine.IsEditorMode()) return -1;
    auto input = std::make_shared<rei::SimulatedInput>();
    try
    {
        const auto events = nlohmann::json::parse(requestJson);
        if (!events.is_array() || events.empty() || events.size() > 128) return -3;
        for (const auto& item : events)
        {
            rei::SimulatedInputEvent event;
            const std::string type = item.at("type");
            if (type == "move") event.Kind = rei::SimulatedInputEvent::Move;
            else if (type == "button_down") event.Kind = rei::SimulatedInputEvent::ButtonDown;
            else if (type == "button_up") event.Kind = rei::SimulatedInputEvent::ButtonUp;
            else if (type == "key_down") event.Kind = rei::SimulatedInputEvent::KeyDown;
            else if (type == "key_up") event.Kind = rei::SimulatedInputEvent::KeyUp;
            else if (type == "scroll") event.Kind = rei::SimulatedInputEvent::Scroll;
            else if (type != "wait") return -3;
            event.X = item.value("x", 0.0);
            event.Y = item.value("y", 0.0);
            event.Code = item.value("code", 0);
            input->Events.push_back(event);
        }
    }
    catch (...) { return -3; }
    auto completion = input->Completion.get_future();
    engine.ExecuteOnMainThread([input] { rei::Input::StartSimulation(input); });
    const auto deadline = std::chrono::steady_clock::now() + std::chrono::seconds(10);
    while (completion.wait_for(std::chrono::milliseconds(10)) != std::future_status::ready)
    {
        if (!engine.IsRunning() || std::chrono::steady_clock::now() >= deadline)
        {
            input->Cancelled = true;
            return -4;
        }
    }
    return completion.get();
}
