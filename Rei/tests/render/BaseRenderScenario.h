#pragma once

class BaseRenderScenario
{
public:
    explicit BaseRenderScenario(GLFWwindow* target)
        : _target(target)
    {
    }

    virtual void Setup() = 0;
    virtual void Render() = 0;
    virtual void Dispose() = 0;

protected:
    GLFWwindow* _target;
};