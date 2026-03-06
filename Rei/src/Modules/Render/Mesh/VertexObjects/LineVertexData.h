#pragma once
#include "glad/glad.h"

class LineVertexData
{
public:
    unsigned int _vertexBuffer, _vertexArray;

public:
    LineVertexData()
    {
        glGenVertexArrays(1, &_vertexArray);
        glGenBuffers(1, &_vertexBuffer);

        glBindVertexArray(_vertexArray);

        glBindBuffer(GL_ARRAY_BUFFER, _vertexBuffer);
        float vertices[] = {
            0.0f, 0.0f, 0.0f,   1.0f, 1.0f, 1.0f
        };
        glBufferData(GL_ARRAY_BUFFER, sizeof(vertices), vertices, GL_STATIC_DRAW);
        glVertexAttribPointer(0, 3, GL_FLOAT, GL_FALSE, 3 * sizeof(float), static_cast<void*>(nullptr));
        glEnableVertexAttribArray(0);

        glBindBuffer(GL_ARRAY_BUFFER, 0);
        glBindVertexArray(0);
    }

    ~LineVertexData()
    {
        glDeleteVertexArrays(1, &_vertexArray);
        glDeleteBuffers(1, &_vertexBuffer);
    }

    void Render(const f32 lineWidth = 1) const
    {
        glLineWidth(lineWidth);
        
        glBindVertexArray(_vertexArray);
        glDrawArrays(GL_LINES, 0, 2);
        glBindVertexArray(0);
    }
};
