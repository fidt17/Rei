#pragma once

#include <span>

#include "Common/Math/Vector2.h"
#include "Modules/Render/Color/Color.h"

namespace rei::render
{
    struct QuadInstance
    {
        math::Vector2 Center;
        math::Vector2 Size;
        Color Tint;
    };

    class REI_API InstancedQuadBatch
    {
    public:
        InstancedQuadBatch() = default;
        InstancedQuadBatch(const InstancedQuadBatch&) = delete;
        InstancedQuadBatch& operator=(const InstancedQuadBatch&) = delete;

        void Setup(u32 capacity);
        void SetInstances(std::span<const QuadInstance> instances);
        void Render() const;
        void Dispose();

        u32 GetInstanceCount() const { return _instanceCount; }
        u32 GetCapacity() const { return _capacity; }

    private:
        u32 _vertexArray = 0;
        u32 _vertexBuffer = 0;
        u32 _indexBuffer = 0;
        u32 _instanceBuffer = 0;
        u32 _instanceCount = 0;
        u32 _capacity = 0;
    };
}
