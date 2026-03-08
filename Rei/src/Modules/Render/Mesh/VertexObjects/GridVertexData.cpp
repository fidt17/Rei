#include "pch.h"
#include "GridVertexData.h"

#include "glad/glad.h"

GridVertexData::GridVertexData(const f32 size, const f32 cellSize)
    : _size(size), _cellSize(cellSize)
{
    std::vector<f32> vertices;
    std::vector<u32> indices;

    i32 idxOffset = 0;

    const f32 halfCellSize = cellSize / 2.0f;
    const f32 lineHalfWidth = size / 2;
    const i32 segments = size / cellSize + 1;

    const f32 posOffset = -size / 2;
    for (i32 i = 0; i < segments * 2; i += 2)
    {
        f32 xPos = i * (halfCellSize) + posOffset;

        vertices.push_back(xPos);
        vertices.push_back(-lineHalfWidth);
        vertices.push_back(0);

        vertices.push_back(xPos);
        vertices.push_back(lineHalfWidth);
        vertices.push_back(0);

        indices.push_back(i + idxOffset);
        indices.push_back(i + 1 + idxOffset);
    }

    idxOffset = indices.size();
    for (i32 i = 0; i < segments * 2; i += 2)
    {
        f32 yPos = i * (halfCellSize) + posOffset;

        vertices.push_back(-lineHalfWidth);
        vertices.push_back(yPos);
        vertices.push_back(0);

        vertices.push_back(lineHalfWidth);
        vertices.push_back(yPos);
        vertices.push_back(0);

        indices.push_back(i + idxOffset);
        indices.push_back(i + 1 + idxOffset);
    }

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

GridVertexData::~GridVertexData()
{
    glDeleteVertexArrays(1, &VAO);
    glDeleteBuffers(1, &VBO);
    glDeleteBuffers(1, &EBO);
}

void GridVertexData::Render() const
{
    glBindVertexArray(VAO);
    glDrawElements(GL_LINES, _indicesCount, GL_UNSIGNED_INT, nullptr);
    glBindVertexArray(0);
}

f32 GridVertexData::GetSize() const
{
    return _size;
}

f32 GridVertexData::GetCellSize() const
{
    return _cellSize;
}
