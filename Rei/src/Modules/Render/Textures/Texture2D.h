#pragma once

namespace rei::render
{

    
    class Texture2D
    {
    public:
        explicit Texture2D(resources::BinaryReader& reader);

        void Use() const;

    private:
        u32 _id;
    };
}
