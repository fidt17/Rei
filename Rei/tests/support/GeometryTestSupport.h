#pragma once
#include "NativeTestSupport.h"
#include "Common/Math/math.h"
#include "Modules/Render/Mesh/Mesh.h"
#include "glm/gtc/quaternion.hpp"
#include <cmath>

namespace rei::tests
{
    inline void CheckVector(const math::Vector3& actual, const math::Vector3& expected, const f32 epsilon = 1e-4f)
    {
        CAPTURE(actual.x, actual.y, actual.z, expected.x, expected.y, expected.z);
        CHECK(actual.x == Catch::Approx(expected.x).margin(epsilon));
        CHECK(actual.y == Catch::Approx(expected.y).margin(epsilon));
        CHECK(actual.z == Catch::Approx(expected.z).margin(epsilon));
    }

    inline void CheckRotation(const glm::quat& actual, const glm::quat& expected)
    {
        REQUIRE(std::isfinite(actual.w));
        REQUIRE(std::isfinite(actual.x));
        REQUIRE(std::isfinite(actual.y));
        REQUIRE(std::isfinite(actual.z));
        CHECK(glm::length(actual) == Catch::Approx(1.0f).margin(1e-4f));
        CHECK(std::abs(glm::dot(actual, expected)) == Catch::Approx(1.0f).margin(1e-4f));
    }

    // Right triangle with analytic footprint x >= origin.x, y >= origin.y,
    // (x - origin.x) + (y - origin.y) <= 2. No production intersection oracle.
    inline render::Face Triangle(const math::Vector3& origin = {0, 0, 5})
    {
        render::Face face;
        face.Vertices = {render::Vertex{glm::vec3(origin)}, render::Vertex{glm::vec3(origin + math::Vector3(2, 0, 0))}, render::Vertex{glm::vec3(origin + math::Vector3(0, 2, 0))}};
        return face;
    }

    inline render::Mesh MeshFromFaces(const std::vector<render::Face>& faces)
    {
        std::vector<render::Vertex> vertices;
        std::vector<u32> indices;
        for (const auto& face : faces)
        {
            for (const auto& vertex : face.Vertices)
            {
                indices.push_back(static_cast<u32>(vertices.size()));
                vertices.push_back(vertex);
            }
        }
        return render::Mesh("test", vertices, indices);
    }

}
