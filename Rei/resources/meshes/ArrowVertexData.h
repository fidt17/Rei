#pragma once
#include "glad/glad.h"

class ArrowVertexData
{
private:
    u32 VAO;
    u32 VBO;
    u32 EBO;
    i32 _indicesCount = 0;
    i32 _verticesCount = 0;

public:
    ArrowVertexData() = default;

    i32 AddVertex(std::vector<float>& vertices, const f32 x, const f32 y, const f32 z)
    {
        vertices.push_back(x);
        vertices.push_back(y);
        vertices.push_back(z);

        return _verticesCount++;
    }

    void ConstructTriangle(std::vector<i32>& indices, const i32 a, const i32 b, const i32 c)
    {
        indices.push_back(a);
        indices.push_back(b);
        indices.push_back(c);
        _indicesCount++;
    }

    explicit ArrowVertexData(const f32 height, const f32 headHeight, const f32 headRadius, const f32 cylinderRadius)
    {
        constexpr i32 segments = 16;

        std::vector<float> vertices;
        std::vector<i32> indices;

        // cylinder walls
        for (i32 i = 0; i < segments; i++)
        {
            const f32 angle = PI * 2.0f * i / segments;

            AddVertex(vertices, std::cos(angle) * cylinderRadius, std::sin(angle) * cylinderRadius, 0);
            AddVertex(vertices, std::cos(angle) * cylinderRadius, std::sin(angle) * cylinderRadius, height - headHeight);
            
            if (i == 0) continue;

            ConstructTriangle(indices, _verticesCount - 4, _verticesCount - 2, _verticesCount - 1);
            ConstructTriangle(indices, _verticesCount - 4, _verticesCount - 3, _verticesCount - 1);
        }

        ConstructTriangle(indices, _verticesCount - 2, _verticesCount - 1, 0);
        ConstructTriangle(indices, 0, _verticesCount - 1, 1);
        // ---

        // cylinder bottom
        const i32 bottomCenter = AddVertex(vertices, 0, 0, 0);
        for (i32 i = 0; i < segments * 2; i += 2)
        {
            if (i != segments * 2 - 2)
            {
                ConstructTriangle(indices, i, bottomCenter, i + 2);
            }
            else
            {
                ConstructTriangle(indices, i, bottomCenter, 0);
            }
        }
        // ---

        // pyramid bottom
        const auto headCircleStart = _verticesCount + 1;
        for (i32 i = 0; i < segments; i++)
        {
            const f32 angle = PI * 2.0f * i / segments;

            AddVertex(vertices, std::cos(angle) * cylinderRadius, std::sin(angle) * cylinderRadius, height - headHeight);
            AddVertex(vertices, std::cos(angle) * headRadius, std::sin(angle) * headRadius, height - headHeight);
            
            if (i == 0) continue;

            ConstructTriangle(indices, _verticesCount - 4, _verticesCount - 2, _verticesCount - 1);
            ConstructTriangle(indices, _verticesCount - 4, _verticesCount - 3, _verticesCount - 1);
        }
        
        ConstructTriangle(indices, _verticesCount - 2, _verticesCount - 1, headCircleStart);
        ConstructTriangle(indices, headCircleStart, _verticesCount - 2, headCircleStart - 1);
        // ---

        // pyramid walls
        const i32 topCenter = AddVertex(vertices, 0, 0, height);
        for (i32 i = 0; i < segments * 2; i += 2)
        {
            auto idx = headCircleStart + i;
            if (i != segments * 2 - 2)
            {
                ConstructTriangle(indices, idx, topCenter, idx + 2);
            }
            else
            {
                ConstructTriangle(indices, idx, topCenter, headCircleStart);
            }
        }
        // ---

        _indicesCount = static_cast<i32>(indices.size());

        glGenVertexArrays(1, &VAO);
        glBindVertexArray(VAO);

        glGenBuffers(1, &VBO);
        glBindBuffer(GL_ARRAY_BUFFER, VBO);
        glBufferData(GL_ARRAY_BUFFER, vertices.size() * sizeof(f32), vertices.data(), GL_STATIC_DRAW);

        glGenBuffers(1, &EBO);
        glBindBuffer(GL_ELEMENT_ARRAY_BUFFER, EBO);
        glBufferData(GL_ELEMENT_ARRAY_BUFFER, indices.size() * sizeof(u32), indices.data(), GL_STATIC_DRAW);

        glVertexAttribPointer(0, 3, GL_FLOAT, GL_FALSE, 3 * sizeof(f32), static_cast<void*>(nullptr));
        glEnableVertexAttribArray(0);

        glBindVertexArray(0);

        glBindBuffer(GL_ARRAY_BUFFER, 0);
        glBindBuffer(GL_ELEMENT_ARRAY_BUFFER, 0);
    }

    ~ArrowVertexData()
    {
        glDeleteVertexArrays(1, &VAO);
        glDeleteBuffers(1, &VBO);
        glDeleteBuffers(1, &EBO);
    }

    void Render() const
    {
        glBindVertexArray(VAO);
        glDrawElements(GL_TRIANGLES, _indicesCount, GL_UNSIGNED_INT, nullptr);
        glBindVertexArray(0);
    }
};
