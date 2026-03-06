#pragma once

#include <string>

namespace rei::render
{
    class ShaderGenerator
    {
    public:
        REI_API static ShaderGenerator& GetInstance();

        REI_API void Initialize();

        REI_API std::string ComposeVertexSource(const std::string& content) const;
        REI_API std::string ComposeFragmentSource(const std::string& content) const;

    private:
        bool _isInitialized = false;
        std::string _commonIncludes;
        std::string _vertexIncludes;
        std::string _fragmentIncludes;
    };
}
