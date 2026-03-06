#pragma once
#include "BaseVertexObject.h"

namespace rei::render
{
    class CubeVertexObject : public BaseVertexObject
    {
    public:
        explicit CubeVertexObject(const math::Vector3& center, const math::Vector3& size);

    protected:
        std::string GetMeshName() const override;
    };
}
