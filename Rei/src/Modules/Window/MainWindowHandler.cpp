#include "pch.h"
#include "MainWindowHandler.h"

namespace rei::window
{
    std::shared_ptr<Window> MainWindowHandler::CreateMainWindow(WindowManager& windowManager, const WindowCreationSettings& settings)
    {
         REI_ASSERT_S(_window == nullptr)
 
         _window = windowManager.NewWindow(settings);
 
         _window->CloseRequestEvent.append([&]
         {
             windowManager.CloseWindow(*_window);
         });
 
         _window->WindowClosedEvent.append([&](const Window&)
         {
             MainWindowClosedEvent();
         });
 
         return _window;
    }

    std::shared_ptr<Window> MainWindowHandler::GetMainWindow()
    {
        return _window;
    }

    bool MainWindowHandler::IsSet() const
    {
        return _window != nullptr;
    }
}
