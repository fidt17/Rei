#pragma once
#include "BaseVertexObject.h"

namespace rei::render
{
    class RingVertexObject : public BaseVertexObject
    {
    public:
        RingVertexObject(f32 radius, f32 width, i32 segments = 64, f32 thickness = 0.05f);

    protected:
        std::string GetMeshName() const override;
    };
}
