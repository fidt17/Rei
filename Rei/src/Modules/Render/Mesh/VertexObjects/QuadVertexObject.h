#pragma once
#include "BaseVertexObject.h"

namespace rei::render
{
    class QuadVertexObject : public BaseVertexObject
    {
    public:
        QuadVertexObject(f32 width, f32 height);

    protected:
        std::string GetMeshName() const override;
    };
}
