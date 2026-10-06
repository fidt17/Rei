#pragma once
#include "Modules/Render/Color/Color.h"
#include <vector>
#include <unordered_map>

namespace rei::render
{
    class Shader
    {
    public:
        // Batches may nest/interleave. Standalone setters still bind their program.
        class UniformBatch
        {
        public:
            REI_API explicit UniformBatch(const Shader& shader, bool bindProgram = true);
            REI_API ~UniformBatch();
            UniformBatch(const UniformBatch&) = delete;
            UniformBatch& operator=(const UniformBatch&) = delete;

        private:
            const Shader* _previous;
        };

        REI_API Shader() = default;
        REI_API explicit Shader(resources::BinaryReader& reader);
        Shader(const Shader& other) = delete;
        Shader& operator=(const Shader& other) = delete;
        REI_API Shader(Shader&& other) noexcept;
        REI_API Shader& operator=(Shader&& other) noexcept;
        REI_API ~Shader();

        REI_API void Use() const;
        REI_API void Delete() const;

        REI_API i32 GetLocation(const std::string& name) const;
        REI_API void SetInt(const std::string& name, i32 value) const;
        REI_API void SetFloat(const std::string& name, f32 value) const;
        REI_API void SetVector3(const std::string& name, const math::Vector3& value) const;
        REI_API void SetColor(const std::string& name, const Color& value) const;
        REI_API void SetMatrix4f(const std::string& name, glm::mat4 value) const;

        // Locations belong to this program revision. Linear colors are already decoded.
        REI_API void SetInt(i32 location, i32 value) const;
        REI_API void SetFloat(i32 location, f32 value) const;
        REI_API void SetVector3(i32 location, const math::Vector3& value) const;
        REI_API void SetLinearColor(i32 location, const Color& value) const;
        REI_API void SetMatrix4f(i32 location, const glm::mat4& value) const;
        // Call after external/raw GL program bindings; the next setter binds its program.
        REI_API static void InvalidateProgramBinding();

        REI_API void SetViewMatrices(const glm::mat4& projectionMatrix, const glm::mat4& viewMatrix, const glm::mat4& modelMatrix) const;
        REI_API void PostLoad();
        REI_API u64 GetProgramRevision() const { return _programRevision; }
        // References expire when the program is deleted, moved or recreated.
        REI_API const std::vector<std::string>& GetUniformNamesByType(u32 uniformType) const;
        
        static REI_API Shader CreateInstanceFrom(const Shader& source);
        
    private:
        void CacheUniformNames();
        bool PrepareUniform(i32 location) const;

        mutable u32 _id = 0;
        mutable u64 _programRevision = 0;
        mutable std::unordered_map<std::string, i32> _locations;
        mutable std::unordered_map<u32, std::vector<std::string>> _uniformNamesByType;

        std::string _vertexSource;
        std::string _fragmentSource;
    };
}
