#pragma once

struct WindowCreationSettings
{
    std::string Name;
    i32 Width;
    i32 Height;
    bool HideOnCreation;
    
    bool HideCursor;
    bool CenterCursor;
    bool FullScreen = false;
};
