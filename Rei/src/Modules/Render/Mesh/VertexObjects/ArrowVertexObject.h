#pragma once
#include "BaseVertexObject.h"

namespace rei::render
{
    class ArrowVertexObject : public BaseVertexObject
    {
    public:
        ArrowVertexObject(f32 height, f32 coneHeight, f32 coneRadius, f32 cylinderRadius);

        std::string GetMeshName() const override;
    };
}
