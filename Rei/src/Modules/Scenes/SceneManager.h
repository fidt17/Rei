#pragma once
#include "BuildScenesConfig.h"

namespace rei::scenes
{
    class SceneManager
    {
    public:
        explicit SceneManager();
    
        void LoadScene(int id);

    private:
        BuildScenesConfig _buildScenesConfig;
    };
}
