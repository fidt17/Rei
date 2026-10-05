#include "pch.h"
#include "support/AssetTestSupport.h"
#include "support/GeometryTestSupport.h"
#include "Modules/Assets/Registry/AssetRegistry.h"
#include "Modules/Physics/ModelCollider.h"
#include "Modules/Render/Model/Model.h"
#include <bit>

using namespace rei;
using namespace rei::math;
using namespace rei::render;
using namespace rei::tests;

namespace
{
    void AppendFloat(std::vector<u8>& bytes, const f32 value) { AppendInteger(bytes, std::bit_cast<u32>(value), 4); }

    void AppendVector(std::vector<u8>& bytes, const Vector3& value)
    {
        AppendFloat(bytes, value.x);
        AppendFloat(bytes, value.y);
        AppendFloat(bytes, value.z);
    }

    void AppendVertex(std::vector<u8>& bytes, const Vertex& vertex)
    {
        static_assert(sizeof(Vertex) == 32);
        AppendVector(bytes, Vector3(vertex.Position));
        AppendVector(bytes, {0, 0, -1});
        AppendFloat(bytes, 0);
        AppendFloat(bytes, 0);
    }

    void AppendFaces(std::vector<u8>& bytes, const std::vector<Face>& faces)
    {
        AppendInteger(bytes, faces.size(), 4);
        for (const auto& face : faces)
        {
            AppendInteger(bytes, face.Vertices.size(), 4);
            for (const auto& vertex : face.Vertices) AppendVertex(bytes, vertex);
        }
    }

    std::vector<u8> NodeBytes(const Vector3& min, const Vector3& max, const std::vector<Face>& faces, const std::vector<u8>& left = {}, const std::vector<u8>& right = {})
    {
        std::vector<u8> bytes;
        AppendVector(bytes, min);
        AppendVector(bytes, max);
        AppendFaces(bytes, faces);
        bytes.push_back(left.empty() ? 0 : 1);
        bytes.insert(bytes.end(), left.begin(), left.end());
        bytes.push_back(right.empty() ? 0 : 1);
        bytes.insert(bytes.end(), right.begin(), right.end());
        return bytes;
    }

    std::vector<u8> LeafBytes(const Vector3& origin)
    {
        return NodeBytes(origin - Vector3(0, 0, 0.001f), origin + Vector3(2, 2, 0.001f), {Triangle(origin)});
    }

    std::vector<u8> MeshBytes(const std::string& name, const std::vector<Face>& faces, const std::vector<u8>& bvh)
    {
        std::vector<u8> bytes;
        AppendString(bytes, name);
        AppendInteger(bytes, faces.size() * 3, 4);
        for (const auto& face : faces)
        {
            REQUIRE(face.Vertices.size() == 3);
            for (const auto& vertex : face.Vertices) AppendVertex(bytes, vertex);
        }
        AppendInteger(bytes, faces.size() * 3, 4);
        for (u32 index = 0; index < faces.size() * 3; ++index) AppendInteger(bytes, index, 4);
        AppendFaces(bytes, faces);
        bytes.insert(bytes.end(), bvh.begin(), bvh.end());
        return bytes;
    }

    std::vector<u8> ModelBytes(const std::vector<std::vector<u8>>& meshes)
    {
        std::vector<u8> bytes;
        AppendString(bytes, "native-packed-model");
        AppendInteger(bytes, meshes.size(), 4);
        for (const auto& mesh : meshes) bytes.insert(bytes.end(), mesh.begin(), mesh.end());
        return bytes;
    }

    std::vector<u8> SplitMeshBytes()
    {
        const auto root = NodeBytes({0, 0, 4.999f}, {5, 2, 10.001f}, {}, LeafBytes({0, 0, 5}), LeafBytes({3, 0, 10}));
        return MeshBytes("split", {Triangle({0, 0, 5}), Triangle({3, 0, 10})}, root);
    }

    void CheckAnalyticTriangleAtStage(const std::string& stage, const std::function<bool(const Ray&, const glm::mat4&, Vector3&)>& intersect)
    {
        struct Probe
        {
            Ray Query;
            glm::mat4 Matrix;
            bool ExpectedHit;
            Vector3 ExpectedPoint;
        };
        // Source footprint: x>=0, y>=0, x+y<=2 on z=5. Expected points
        // below follow that equation and transform x'=10-3y, y'=-2+2x,
        // z'=z+3; no stage acts as oracle for another stage.
        const auto transformed = GetTransformationMatrix({10, -2, 3}, GetQuaternion(Vector3(0, 0, 90)), {2, 3, 1});
        const std::vector<Probe> probes{
            {Ray({0.5f, 0.5f, 0}, {0, 0, 2}), glm::mat4(1), true, {0.5f, 0.5f, 5}},
            {Ray({0.5f, 0.5f, 10}, {0, 0, -0.5f}), glm::mat4(1), true, {0.5f, 0.5f, 5}},
            {Ray({0.5f, 1.75f, 0}, {0, 0, 2}), glm::mat4(1), false, {}},
            {Ray({8.5f, -1, 3}, {0, 0, 2}), transformed, true, {8.5f, -1, 8}},
            {Ray({7.75f, 1.5f, 3}, {0, 0, 2}), transformed, false, {}}
        };
        for (size_t index = 0; index < probes.size(); ++index)
        {
            CAPTURE(stage, index);
            const auto& probe = probes[index];
            Vector3 point(91, 92, 93);
            const bool hit = intersect(probe.Query, probe.Matrix, point);
            CHECK(hit == probe.ExpectedHit);
            CheckVector(point, probe.ExpectedHit ? probe.ExpectedPoint : Vector3(91, 92, 93));
        }
    }

    void LoadPackedModel(assets::AssetRegistry& registry, assets::AssetRef<Model>& ref, TemporaryDirectory& files, const std::vector<u8>& bytes)
    {
        const auto path = files.Write("model.bin", bytes);
        resources::BinaryReader reader(path.string());
        auto model = std::make_unique<Model>(reader);
        REQUIRE(reader.GetPosition() == static_cast<i64>(bytes.size()));
        registry.CreateAssetRecord(ref, "packed", model.release(), static_cast<i32>(bytes.size()), assets::AssetState::Loaded);
        registry.SetRefCount(ref.Id, 1);
        // Binary loading deliberately skips PostLoad: actual ModelCollider and
        // serialized BVH run on CPU. This does not claim GL/rendering coverage.
    }
}

TEST_CASE("PHY-03 Serialized split BVH preserves faces children and analytic hits", "[native][physics][bvh][binary][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        TemporaryDirectory files;
        const auto bytes = SplitMeshBytes();
        resources::BinaryReader reader(files.Write("mesh.bin", bytes).string());
        Mesh mesh(reader);
        REQUIRE(reader.GetPosition() == static_cast<i64>(bytes.size()));
        REQUIRE(mesh.Name == "split");
        REQUIRE(mesh.Vertices.size() == 6);
        CHECK(mesh.Indices == std::vector<u32>{0, 1, 2, 3, 4, 5});
        REQUIRE(mesh.Faces.size() == 2);
        REQUIRE(mesh.BVHRoot.Faces.empty());
        REQUIRE(mesh.BVHRoot.Left != nullptr);
        REQUIRE(mesh.BVHRoot.Right != nullptr);
        REQUIRE(mesh.BVHRoot.Left->Faces.size() == 1);
        REQUIRE(mesh.BVHRoot.Right->Faces.size() == 1);
        CheckVector(Vector3(mesh.BVHRoot.Right->Faces[0].Vertices[0].Position), {3, 0, 10});
        CHECK(mesh.VAO == 0);
        CHECK(mesh.VBO == 0);
        CHECK(mesh.EBO == 0);
        Vector3 point;
        REQUIRE(mesh.BVHRoot.IsRayIntersecting(Ray({0.5f, 0.5f, 0}, {0, 0, 1}), glm::mat4(1), point));
        CheckVector(point, {0.5f, 0.5f, 5});
        REQUIRE(mesh.BVHRoot.IsRayIntersecting(Ray({3.5f, 0.5f, 0}, {0, 0, 1}), glm::mat4(1), point));
        CheckVector(point, {3.5f, 0.5f, 10});
        point = {91, 92, 93};
        CHECK_FALSE(mesh.BVHRoot.IsRayIntersecting(Ray({2.5f, 0.5f, 0}, {0, 0, 1}), glm::mat4(1), point));
        CheckVector(point, {91, 92, 93});
    });
}

TEST_CASE("PHY-04 Loaded packed model collider traverses real mesh BVH and transform", "[native][physics][bvh][assets][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        TemporaryDirectory files;
        assets::AssetRegistry registry;
        assets::AssetRef<Model> ref("packed-model");
        LoadPackedModel(registry, ref, files, ModelBytes({SplitMeshBytes()}));
        physics::ModelCollider collider;
        collider.SetModel(ref);
        REQUIRE(ref.IsLoaded());
        Vector3 point;
        REQUIRE(collider.Intersect(Ray({3.5f, 0.5f, 0}, {0, 0, 1}), glm::mat4(1), point));
        CheckVector(point, {3.5f, 0.5f, 10});
        const auto matrix = GetTransformationMatrix({10, -2, 3}, GetQuaternion(Vector3(0, 0, 90)), {2, 3, 1});
        REQUIRE(collider.Intersect(Ray({8.5f, -1, 3}, {0, 0, 1}), matrix, point));
        CheckVector(point, {8.5f, -1, 8});
        point = {91, 92, 93};
        CHECK_FALSE(collider.Intersect(Ray({50, 50, 0}, {0, 0, 1}), matrix, point));
        CheckVector(point, {91, 92, 93});
        registry.ReleaseAssetWithId(ref.Id);
    });
}

TEST_CASE("PHY-04 Loaded model collider continues to a later matching mesh", "[native][physics][bvh][assets][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        TemporaryDirectory files;
        assets::AssetRegistry registry;
        assets::AssetRef<Model> ref("multi-mesh");
        const auto miss = MeshBytes("first", {Triangle({100, 100, 5})}, LeafBytes({100, 100, 5}));
        const auto hit = MeshBytes("second", {Triangle({0, 0, 9})}, LeafBytes({0, 0, 9}));
        LoadPackedModel(registry, ref, files, ModelBytes({miss, hit}));
        physics::ModelCollider collider;
        collider.SetModel(ref);
        REQUIRE(ref->GetMeshes().size() == 2);
        Vector3 point;
        REQUIRE(collider.Intersect(Ray({0.5f, 0.5f, 0}, {0, 0, 1}), glm::mat4(1), point));
        CheckVector(point, {0.5f, 0.5f, 9});
        registry.ReleaseAssetWithId(ref.Id);
    });
}

TEST_CASE("PHY-04 Retained collider follows release and replacement packed geometry", "[native][physics][bvh][assets][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        TemporaryDirectory files;
        assets::AssetRegistry registry;
        assets::AssetRef<Model> ref("reloaded-model");
        LoadPackedModel(registry, ref, files, ModelBytes({MeshBytes("first", {Triangle()}, LeafBytes({0, 0, 5}))}));
        physics::ModelCollider collider;
        collider.SetModel(ref);
        Vector3 point;
        REQUIRE(collider.Intersect(Ray({0.5f, 0.5f, 0}, {0, 0, 1}), glm::mat4(1), point));
        CheckVector(point, {0.5f, 0.5f, 5});
        registry.ReleaseAssetWithId(ref.Id);
        REQUIRE_FALSE(ref.IsLoaded());
        point = {91, 92, 93};
        CHECK_FALSE(collider.Intersect(Ray({0.5f, 0.5f, 0}, {0, 0, 1}), glm::mat4(1), point));
        CheckVector(point, {91, 92, 93});
        LoadPackedModel(registry, ref, files, ModelBytes({MeshBytes("replacement", {Triangle({0, 0, 9})}, LeafBytes({0, 0, 9}))}));
        REQUIRE(collider.Intersect(Ray({0.5f, 0.5f, 0}, {0, 0, 1}), glm::mat4(1), point));
        CheckVector(point, {0.5f, 0.5f, 9});
        registry.ReleaseAssetWithId(ref.Id);
    });
}

TEST_CASE("PHY-03 Truncated serialized BVH fails explicitly within child deadline", "[native][physics][bvh][binary][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        TemporaryDirectory files;
        auto bytes = SplitMeshBytes();
        bytes.resize(bytes.size() - 20);
        resources::BinaryReader reader(files.Write("truncated-bvh.bin", bytes).string());
        CHECK_THROWS(Mesh(reader));
    });
}

TEST_CASE("PHY-03 One analytic triangle survives runtime serialized packed and rebound collider stages", "[native][physics][bvh][binary][assets][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        TemporaryDirectory files;
        const auto triangle = Triangle();
        const std::vector<Face> faces{triangle};
        CheckAnalyticTriangleAtStage("direct face", [&](const Ray& ray, const glm::mat4& matrix, Vector3& point)
        {
            return FaceRayIntersection(triangle, ray, matrix, point);
        });
        Mesh runtime("analytic", triangle.Vertices, {0, 1, 2}, faces);
        runtime.BVHRoot.BuildBVH(runtime.BVHRoot, faces);
        CheckAnalyticTriangleAtStage("runtime BVH", [&](const Ray& ray, const glm::mat4& matrix, Vector3& point)
        {
            return runtime.BVHRoot.IsRayIntersecting(ray, matrix, point);
        });
        const auto meshBytes = MeshBytes("analytic", faces, LeafBytes({0, 0, 5}));
        resources::BinaryReader meshReader(files.Write("analytic-mesh.bin", meshBytes).string());
        Mesh serialized(meshReader);
        REQUIRE(meshReader.GetPosition() == static_cast<i64>(meshBytes.size()));
        CheckAnalyticTriangleAtStage("serialized BVH", [&](const Ray& ray, const glm::mat4& matrix, Vector3& point)
        {
            return serialized.BVHRoot.IsRayIntersecting(ray, matrix, point);
        });
        const auto modelBytes = ModelBytes({meshBytes});
        WriteAssetPack(files, {{"analytic-packed", "analytic", modelBytes}});
        // Read actual pack payload at its independent, unaligned offset. Skip
        // PostLoad because it sets up GL; this chain proves CPU geometry only.
        resources::BinaryReader packReader(files.File("pack.bin").string(), 3);
        auto packedModel = std::make_unique<Model>(packReader);
        REQUIRE(packReader.GetPosition() == static_cast<i64>(3 + modelBytes.size()));
        REQUIRE(packedModel->GetMeshes().size() == 1);
        assets::AssetRegistry registry;
        assets::AssetRef<Model> first("analytic-packed");
        registry.CreateAssetRecord(first, "analytic", packedModel.release(), static_cast<i32>(modelBytes.size()), assets::AssetState::Loaded);
        registry.SetRefCount(first.Id, 1);
        physics::ModelCollider collider;
        collider.SetModel(first);
        CheckAnalyticTriangleAtStage("packed loaded model collider", [&](const Ray& ray, const glm::mat4& matrix, Vector3& point)
        {
            return collider.Intersect(ray, matrix, point);
        });
        registry.ReleaseAssetWithId(first.Id);
        REQUIRE_FALSE(first.IsLoaded());
        Vector3 point(91, 92, 93);
        CHECK_FALSE(collider.Intersect(Ray({0.5f, 0.5f, 0}, {0, 0, 2}), glm::mat4(1), point));
        CheckVector(point, {91, 92, 93});
        assets::AssetRef<Model> replacement("analytic-rebound");
        LoadPackedModel(registry, replacement, files, modelBytes);
        REQUIRE(replacement.IsLoaded());
        CHECK_FALSE(collider.Intersect(Ray({0.5f, 0.5f, 0}, {0, 0, 2}), glm::mat4(1), point));
        CheckVector(point, {91, 92, 93}); // New record alone does not change binding.
        collider.SetModel(replacement);
        CheckAnalyticTriangleAtStage("rebound replacement collider", [&](const Ray& ray, const glm::mat4& matrix, Vector3& hit)
        {
            return collider.Intersect(ray, matrix, hit);
        });
        registry.ReleaseAssetWithId(replacement.Id);
        REQUIRE_FALSE(replacement.IsLoaded());
    });
}
