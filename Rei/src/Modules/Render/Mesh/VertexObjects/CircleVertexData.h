#pragma once
#include "glad/glad.h"

class CircleVertexData
{
private:
    u32 VAO;
    u32 VBO;
    u32 EBO;
    i32 _indicesCount;

public:
    CircleVertexData() = default;
    
    explicit CircleVertexData(const i32 segments)
    {
        std::vector<float> vertices;
        std::vector<u32> indices;

        indices.push_back(0);
        for (i32 i = 0; i <= segments; i++)
        {
            const f32 angle = PI * 2.0f * i / segments;

            vertices.push_back(std::cos(angle));
            vertices.push_back(std::sin(angle));
            vertices.push_back(0.0f);

            if (i != 0)
            {
                indices.push_back(i);
                indices.push_back(i);
            }
        }
        indices.push_back(0);

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

    ~CircleVertexData()
    {
        glDeleteVertexArrays(1, &VAO);
        glDeleteBuffers(1, &VBO);
        glDeleteBuffers(1, &EBO);
    }

    void Render() const
    {
        glBindVertexArray(VAO);
        glDrawElements(GL_LINES, _indicesCount, GL_UNSIGNED_INT, nullptr);
        glBindVertexArray(0);
    }
};
