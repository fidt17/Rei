#include "pch.h"
#include "InstancedQuadBatch.h"

#include <array>
#include <cstddef>
#include <type_traits>

#include "glad/glad.h"

namespace rei::render
{
    void InstancedQuadBatch::Setup(const u32 capacity)
    {
        REI_THROW_IF(capacity == 0, "Instanced quad batch capacity must be greater than zero")
        if (_vertexArray != 0) return;

        constexpr std::array<f32, 20> VERTICES = {
            -0.5f, -0.5f, 0.0f, 0.0f, 0.0f,
             0.5f, -0.5f, 0.0f, 1.0f, 0.0f,
             0.5f,  0.5f, 0.0f, 1.0f, 1.0f,
            -0.5f,  0.5f, 0.0f, 0.0f, 1.0f
        };
        constexpr std::array<u32, 6> INDICES = { 0, 1, 2, 2, 3, 0 };

        static_assert(std::is_standard_layout_v<QuadInstance>);

        glGenVertexArrays(1, &_vertexArray);
        glGenBuffers(1, &_vertexBuffer);
        glGenBuffers(1, &_indexBuffer);
        glGenBuffers(1, &_instanceBuffer);

        glBindVertexArray(_vertexArray);

        glBindBuffer(GL_ARRAY_BUFFER, _vertexBuffer);
        glBufferData(GL_ARRAY_BUFFER, sizeof(VERTICES), VERTICES.data(), GL_STATIC_DRAW);
        glEnableVertexAttribArray(0);
        glVertexAttribPointer(0, 3, GL_FLOAT, GL_FALSE, 5 * sizeof(f32), nullptr);
        glEnableVertexAttribArray(2);
        glVertexAttribPointer(2, 2, GL_FLOAT, GL_FALSE, 5 * sizeof(f32), reinterpret_cast<void*>(3 * sizeof(f32)));

        glBindBuffer(GL_ELEMENT_ARRAY_BUFFER, _indexBuffer);
        glBufferData(GL_ELEMENT_ARRAY_BUFFER, sizeof(INDICES), INDICES.data(), GL_STATIC_DRAW);

        glBindBuffer(GL_ARRAY_BUFFER, _instanceBuffer);
        glBufferData(GL_ARRAY_BUFFER, capacity * sizeof(QuadInstance), nullptr, GL_DYNAMIC_DRAW);

        glEnableVertexAttribArray(3);
        glVertexAttribPointer(3, 2, GL_FLOAT, GL_FALSE, sizeof(QuadInstance), reinterpret_cast<void*>(offsetof(QuadInstance, Center)));
        glVertexAttribDivisor(3, 1);

        glEnableVertexAttribArray(4);
        glVertexAttribPointer(4, 2, GL_FLOAT, GL_FALSE, sizeof(QuadInstance), reinterpret_cast<void*>(offsetof(QuadInstance, Size)));
        glVertexAttribDivisor(4, 1);

        glEnableVertexAttribArray(5);
        glVertexAttribPointer(5, 4, GL_FLOAT, GL_FALSE, sizeof(QuadInstance), reinterpret_cast<void*>(offsetof(QuadInstance, Tint)));
        glVertexAttribDivisor(5, 1);

        glEnableVertexAttribArray(6);
        glVertexAttribPointer(6, 2, GL_FLOAT, GL_FALSE, sizeof(QuadInstance), reinterpret_cast<void*>(offsetof(QuadInstance, UvMin)));
        glVertexAttribDivisor(6, 1);

        glEnableVertexAttribArray(7);
        glVertexAttribPointer(7, 2, GL_FLOAT, GL_FALSE, sizeof(QuadInstance), reinterpret_cast<void*>(offsetof(QuadInstance, UvMax)));
        glVertexAttribDivisor(7, 1);

        glBindBuffer(GL_ARRAY_BUFFER, 0);
        glBindVertexArray(0);

        _capacity = capacity;
    }

    void InstancedQuadBatch::SetInstances(const std::span<const QuadInstance> instances)
    {
        REI_THROW_IF(_vertexArray == 0, "Instanced quad batch is not set up")
        REI_THROW_IF(instances.size() > _capacity, "Instance count exceeds instanced quad batch capacity")

        glBindBuffer(GL_ARRAY_BUFFER, _instanceBuffer);
        glBufferSubData(GL_ARRAY_BUFFER, 0, instances.size_bytes(), instances.data());
        glBindBuffer(GL_ARRAY_BUFFER, 0);

        _instanceCount = static_cast<u32>(instances.size());
    }

    void InstancedQuadBatch::Render() const
    {
        if (_instanceCount == 0) return;

        glBindVertexArray(_vertexArray);
        glDrawElementsInstanced(GL_TRIANGLES, 6, GL_UNSIGNED_INT, nullptr, static_cast<GLsizei>(_instanceCount));
        glBindVertexArray(0);
    }

    void InstancedQuadBatch::Dispose()
    {
        if (_vertexArray == 0) return;

        glDeleteBuffers(1, &_instanceBuffer);
        glDeleteBuffers(1, &_indexBuffer);
        glDeleteBuffers(1, &_vertexBuffer);
        glDeleteVertexArrays(1, &_vertexArray);

        _vertexArray = 0;
        _vertexBuffer = 0;
        _indexBuffer = 0;
        _instanceBuffer = 0;
        _instanceCount = 0;
        _capacity = 0;
    }
}
