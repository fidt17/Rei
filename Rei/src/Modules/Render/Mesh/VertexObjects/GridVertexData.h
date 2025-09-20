#pragma once

class GridVertexData
{
private:
    u32 VAO;
    u32 VBO;
    u32 EBO;
    i32 _indicesCount;

    f32 _size;
    f32 _cellSize;

public:
    GridVertexData() = default;
    
    explicit GridVertexData(f32 size, f32 cellSize);

    ~GridVertexData();

    void Render() const;

    f32 GetSize() const;
    f32 GetCellSize() const;
};
