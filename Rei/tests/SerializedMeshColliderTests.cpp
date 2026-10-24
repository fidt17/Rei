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
        AppendVector(bytes, Vector3(vertex.Normal));
        AppendFloat(bytes, vertex.TexCoords.x);
        AppendFloat(bytes, vertex.TexCoords.y);
    }

    std::vector<u8> NodeBytes(const Vector3& min, const Vector3& max, const std::vector<u32>& triangles, const std::vector<u8>& left = {}, const std::vector<u8>& right = {})
    {
        std::vector<u8> bytes;
        AppendVector(bytes, min);
        AppendVector(bytes, max);
        AppendInteger(bytes, triangles.size(), 4);
        for (const auto triangle : triangles) AppendInteger(bytes, triangle, 4);
        bytes.push_back(left.empty() ? 0 : 1);
        bytes.insert(bytes.end(), left.begin(), left.end());
        bytes.push_back(right.empty() ? 0 : 1);
        bytes.insert(bytes.end(), right.begin(), right.end());
        return bytes;
    }

    std::vector<u8> LeafBytes(const Vector3& origin, const u32 triangle = 0)
    {
        return NodeBytes(origin - Vector3(0, 0, 0.001f), origin + Vector3(2, 2, 0.001f), {triangle});
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
        bytes.insert(bytes.end(), bvh.begin(), bvh.end());
        return bytes;
    }

    std::vector<u8> ModelBytes(const std::vector<std::vector<u8>>& meshes, const std::vector<u8>& bounds = {0})
    {
        std::vector<u8> bytes;
        AppendString(bytes, "native-packed-model");
        AppendInteger(bytes, meshes.size(), 4);
        for (const auto& mesh : meshes) bytes.insert(bytes.end(), mesh.begin(), mesh.end());
        bytes.insert(bytes.end(), bounds.begin(), bounds.end());
        return bytes;
    }

    std::vector<u8> SplitMeshBytes()
    {
        const auto root = NodeBytes({0, 0, 4.999f}, {5, 2, 10.001f}, {}, LeafBytes({0, 0, 5}), LeafBytes({3, 0, 10}, 1));
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
            {Ray({7.75f, 1.5f, 3}, {0, 0, 2}), transformed, false, {}},
            {Ray({9, -0.5f, -5}, {0, 0, 0.25f}), GetTransformationMatrix({10, -2, 3}, glm::quat(1, 0, 0, 0), {-2, 3, -1}), true, {9, -0.5f, -2}},
            {Ray({9, -0.5f, 0}, {0, 0, 0.25f}), GetTransformationMatrix({10, -2, 3}, glm::quat(1, 0, 0, 0), {-2, 3, 1}), true, {9, -0.5f, 8}},
            {Ray({0.00005f, 0.00005f, 0}, {0, 0, 100}), glm::scale(glm::mat4(1), glm::vec3(0.0001f)), true, {0.00005f, 0.00005f, 0.0005f}},
            {Ray({500, 500, 0}, {0, 0, 0.000001f}), glm::scale(glm::mat4(1), glm::vec3(1000)), true, {500, 500, 5000}},
            {Ray({0.000005f, 0.000005f, 0}, {0, 0, 1}), glm::scale(glm::mat4(1), glm::vec3(0.00001f)), false, {}}
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

TEST_CASE("PHY-03 Packed triangles preserve vertex attributes and following stream data", "[native][physics][bvh][binary][isolated]")
{
    Isolated([]
    {
        TemporaryDirectory files;
        auto face = Triangle();
        for (u32 i = 0; i < face.Vertices.size(); ++i)
        {
            face.Vertices[i].Normal = {static_cast<f32>(i), -1, 0.5f};
            face.Vertices[i].TexCoords = {static_cast<f32>(i) / 2, -0.25f};
        }
        auto bytes = MeshBytes("attributes", {face}, NodeBytes({0, 0, 4}, {2, 2, 6}, {0}));
        constexpr u32 SENTINEL = 0x78563412;
        AppendInteger(bytes, SENTINEL, 4);
        resources::BinaryReader reader(files.Write("attributes.bin", bytes).string());
        Mesh mesh(reader);
        REQUIRE(reader.GetPosition() == static_cast<i64>(bytes.size() - 4));
        REQUIRE(reader.GetU32() == SENTINEL);
        REQUIRE(mesh.Indices.size() == 3);
        REQUIRE(mesh.BVHRoot.Triangles == std::vector<u32>{0});
        const auto indexedFace = mesh.GetFace(mesh.BVHRoot.Triangles[0]);
        for (u32 i = 0; i < face.Vertices.size(); ++i)
        {
            for (const auto* actual : std::array<const Vertex*, 2>{&mesh.Vertices[i], &indexedFace.Vertices[i]})
            {
                CheckVector(Vector3(actual->Position), Vector3(face.Vertices[i].Position));
                CheckVector(Vector3(actual->Normal), Vector3(face.Vertices[i].Normal));
                CHECK(actual->TexCoords.x == Catch::Approx(face.Vertices[i].TexCoords.x));
                CHECK(actual->TexCoords.y == Catch::Approx(face.Vertices[i].TexCoords.y));
            }
        }
    });
}

TEST_CASE("PHY-03 Invalid packed vertex and triangle indices reject explicitly", "[native][physics][bvh][binary][isolated]")
{
    Isolated([]
    {
        TemporaryDirectory files;
        for (const auto& triangles : std::vector<std::vector<u32>>{{1}, {0, 0}, {}})
        {
            const auto bytes = MeshBytes("bad-bvh", {Triangle()}, NodeBytes({0, 0, 4}, {2, 2, 6}, triangles));
            resources::BinaryReader reader(files.Write("bad-bvh.bin", bytes).string());
            CHECK_THROWS(Mesh(reader));
        }
        for (const auto& indices : std::vector<std::vector<u32>>{{0}, {0, 1}, {0, 1, 3}, {0, 1, 0xffffffff}})
        {
            std::vector<u8> bytes;
            AppendString(bytes, "bad-indices");
            AppendInteger(bytes, 3, 4);
            for (const auto& vertex : Triangle().Vertices) AppendVertex(bytes, vertex);
            AppendInteger(bytes, indices.size(), 4);
            for (const auto index : indices) AppendInteger(bytes, index, 4);
            resources::BinaryReader reader(files.Write("bad-indices.bin", bytes).string());
            CHECK_THROWS(Mesh(reader));
            CHECK_THROWS(Mesh("invalid", std::vector<Vertex>(3), indices));
        }
        std::vector<u8> invalidModel;
        AppendString(invalidModel, "negative-mesh-count");
        AppendInteger(invalidModel, 0xffffffff, 4);
        resources::BinaryReader reader(files.Write("invalid-model.bin", invalidModel).string());
        CHECK_THROWS_WITH(Model(reader), Catch::Matchers::ContainsSubstring("Negative packed model mesh count"));
    });
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
        REQUIRE(mesh.BVHRoot.Triangles.empty());
        REQUIRE(mesh.BVHRoot.Left != nullptr);
        REQUIRE(mesh.BVHRoot.Right != nullptr);
        REQUIRE(mesh.BVHRoot.Left->Triangles.size() == 1);
        REQUIRE(mesh.BVHRoot.Right->Triangles.size() == 1);
        CheckVector(Vector3(mesh.GetFace(mesh.BVHRoot.Right->Triangles[0]).Vertices[0].Position), {3, 0, 10});
        CHECK(mesh.VAO == 0);
        CHECK(mesh.VBO == 0);
        CHECK(mesh.EBO == 0);
        Vector3 point;
        REQUIRE(mesh.BVHRoot.IsRayIntersecting(mesh, Ray({0.5f, 0.5f, 0}, {0, 0, 1}), glm::mat4(1), point));
        CheckVector(point, {0.5f, 0.5f, 5});
        REQUIRE(mesh.BVHRoot.IsRayIntersecting(mesh, Ray({3.5f, 0.5f, 0}, {0, 0, 1}), glm::mat4(1), point));
        CheckVector(point, {3.5f, 0.5f, 10});
        point = {91, 92, 93};
        CHECK_FALSE(mesh.BVHRoot.IsRayIntersecting(mesh, Ray({2.5f, 0.5f, 0}, {0, 0, 1}), glm::mat4(1), point));
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
        Mesh runtime("analytic", std::vector<Vertex>(triangle.Vertices.begin(), triangle.Vertices.end()), {0, 1, 2});
        runtime.BVHRoot.BuildBVH(runtime);
        CheckAnalyticTriangleAtStage("runtime BVH", [&](const Ray& ray, const glm::mat4& matrix, Vector3& point)
        {
            return runtime.BVHRoot.IsRayIntersecting(runtime, ray, matrix, point);
        });
        const auto meshBytes = MeshBytes("analytic", faces, LeafBytes({0, 0, 5}));
        resources::BinaryReader meshReader(files.Write("analytic-mesh.bin", meshBytes).string());
        Mesh serialized(meshReader);
        REQUIRE(meshReader.GetPosition() == static_cast<i64>(meshBytes.size()));
        CheckAnalyticTriangleAtStage("serialized BVH", [&](const Ray& ray, const glm::mat4& matrix, Vector3& point)
        {
            return serialized.BVHRoot.IsRayIntersecting(serialized, ray, matrix, point);
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

TEST_CASE("PHY-03 Shared reordered vertices survive packed BVH and mesh ownership changes", "[native][physics][bvh][binary][isolated]")
{
    Isolated([]
    {
        TemporaryDirectory files;
        const std::vector<Vertex> vertices{{{0, 0, 5}}, {{2, 0, 5}}, {{0, 2, 5}}, {{2, 2, 5}}};
        const std::vector<u32> indices{2, 0, 1, 2, 1, 3};
        Mesh runtime("shared", vertices, indices);
        runtime.BVHRoot.BuildBVH(runtime);
        std::vector<u8> bytes;
        AppendString(bytes, "shared");
        AppendInteger(bytes, vertices.size(), 4);
        for (const auto& vertex : vertices) AppendVertex(bytes, vertex);
        AppendInteger(bytes, indices.size(), 4);
        for (const auto index : indices) AppendInteger(bytes, index, 4);
        const auto bvh = NodeBytes({0, 0, 4.999f}, {2, 2, 5.001f}, {1, 0});
        bytes.insert(bytes.end(), bvh.begin(), bvh.end());
        resources::BinaryReader reader(files.Write("shared.bin", bytes).string());
        Mesh loaded(reader);
        REQUIRE(loaded.Vertices.size() == 4);
        REQUIRE(loaded.Indices == indices);
        CHECK(loaded.BVHRoot.Triangles == std::vector<u32>{1, 0});
        Mesh copied = loaded;
        loaded = Mesh("released", {}, {});
        Mesh moved = std::move(copied);
        for (const auto* mesh : {&runtime, &moved})
        {
            for (const auto& origin : std::array<Vector3, 2>{{{0.25f, 0.25f, 0}, {1.75f, 1.75f, 0}}})
            {
                Vector3 point;
                REQUIRE(mesh->BVHRoot.IsRayIntersecting(*mesh, Ray(origin, {0, 0, 1}), glm::mat4(1), point));
                CheckVector(point, {origin.x, origin.y, 5});
            }
            Vector3 point(91, 92, 93);
            CHECK_FALSE(mesh->BVHRoot.IsRayIntersecting(*mesh, Ray({3, 3, 0}, {0, 0, 1}), glm::mat4(1), point));
            CheckVector(point, {91, 92, 93});
        }
    });
}

TEST_CASE("PHY-03 Packed BVH rejects invalid counts bounds child flags and excessive depth", "[native][physics][bvh][binary][isolated]")
{
    Isolated([]
    {
        TemporaryDirectory files;
        auto nested = LeafBytes({0, 0, 5});
        for (i32 depth = 0; depth <= MeshBVHNode::MAX_DEPTH; ++depth)
            nested = NodeBytes({0, 0, 4}, {2, 2, 6}, {}, nested);
        auto flag = LeafBytes({0, 0, 5});
        flag[flag.size() - 2] = 2;
        auto negative = LeafBytes({0, 0, 5});
        for (u32 offset = 24; offset < 28; ++offset) negative[offset] = 0xff;
        for (const auto& node : {nested, flag, negative, NodeBytes({2, 0, 4}, {0, 2, 6}, {0})})
        {
            const auto bytes = MeshBytes("invalid-node", {Triangle()}, node);
            resources::BinaryReader reader(files.Write("invalid-node.bin", bytes).string());
            CHECK_THROWS(Mesh(reader));
        }
        const auto emptyBytes = MeshBytes("empty", {}, NodeBytes({}, {}, {}));
        resources::BinaryReader reader(files.Write("empty.bin", emptyBytes).string());
        Mesh empty(reader);
        Vector3 point(91, 92, 93);
        CHECK_FALSE(empty.BVHRoot.IsRayIntersecting(empty, Ray({}, {0, 0, 1}), glm::mat4(1), point));
        CheckVector(point, {91, 92, 93});
    });
}

TEST_CASE("PHY-03 BVH local traversal preserves parallel edge hits and singular misses", "[native][physics][bvh][picking]")
{
    MeshBVHNode root;
    const auto mesh = MeshFromFaces({Triangle()});
    root.BuildBVH(mesh);
    for (const auto& origin : std::array<Vector3, 3>{{{0, 0.5f, 0}, {0.5f, 0, 0}, {1, 1, 0}}})
    {
        Vector3 point;
        REQUIRE(root.IsRayIntersecting(mesh, Ray(origin, {0, 0, 1}), glm::mat4(1), point));
        CheckVector(point, {origin.x, origin.y, 5});
    }
    Vector3 point(91, 92, 93);
    CHECK_FALSE(root.IsRayIntersecting(mesh, Ray({0.5f, 0.5f, 0}, {0, 0, 1}), glm::scale(glm::mat4(1), glm::vec3(1, 1, 0)), point));
    CheckVector(point, {91, 92, 93});
    CHECK_FALSE(root.IsRayIntersecting(mesh, Ray({0.5f, 0.5f, 0}, {}), glm::mat4(1), point));
    CheckVector(point, {91, 92, 93});

    auto shear = glm::mat4(1);
    shear[1][0] = 2; // x' = x + 2y.
    REQUIRE(root.IsRayIntersecting(mesh, Ray({1.5f, 0.5f, 0}, {0, 0, 2}), shear, point));
    CheckVector(point, {1.5f, 0.5f, 5});
}


TEST_CASE("Packed model reads cached bounds and preserves following stream data", "[native][model-bounds][binary][isolated]")
{
    Isolated([]
    {
        TemporaryDirectory files;
        std::vector<u8> bounds{1};
        AppendVector(bounds, {-10, -20, -30});
        AppendVector(bounds, {10, 20, 30});
        auto bytes = ModelBytes({SplitMeshBytes()}, bounds);
        constexpr u32 SENTINEL = 0x78563412;
        AppendInteger(bytes, SENTINEL, 4);
        resources::BinaryReader reader(files.Write("cached-bounds.bin", bytes).string());
        Model model(reader);
        REQUIRE(model.GetBounds().IsValid());
        // Deliberately wider than vertex bounds: loading must use the cache, not rescan geometry.
        CheckVector(Vector3(model.GetBounds().Min), {-10, -20, -30});
        CheckVector(Vector3(model.GetBounds().Max), {10, 20, 30});
        CHECK(reader.GetPosition() == static_cast<i64>(bytes.size() - 4));
        CHECK(reader.GetU32() == SENTINEL);
        REQUIRE(model.GetMeshes().size() == 1);
        CHECK(model.GetMeshes()[0].VAO == 0);
    });
}

TEST_CASE("Packed model unknown bounds remain conservative for empty and nonempty geometry", "[native][model-bounds][binary][isolated]")
{
    Isolated([]
    {
        TemporaryDirectory files;
        for (const auto& meshes : std::vector<std::vector<std::vector<u8>>>{{}, {SplitMeshBytes()}})
        {
            const auto bytes = ModelBytes(meshes);
            resources::BinaryReader reader(files.Write("unknown-bounds.bin", bytes).string());
            Model model(reader);
            CHECK_FALSE(model.GetBounds().IsValid());
            CHECK(model.GetBounds().IntersectsSphere({10000, 10000, 10000}, 1));
            CHECK(reader.GetPosition() == static_cast<i64>(bytes.size()));
        }
    });
}

TEST_CASE("Packed model rejects missing truncated and malformed bounds metadata", "[native][model-bounds][binary][isolated]")
{
    Isolated([]
    {
        TemporaryDirectory files;
        std::vector<u8> complete{1};
        AppendVector(complete, {-1, -2, -3});
        AppendVector(complete, {4, 5, 6});
        REQUIRE(complete.size() == 25);
        for (size_t size = 0; size < complete.size(); ++size)
        {
            CAPTURE(size);
            const std::vector<u8> truncated(complete.begin(), complete.begin() + size);
            resources::BinaryReader reader(files.Write("truncated-bounds.bin", ModelBytes({}, truncated)).string());
            CHECK_THROWS_WITH(Model(reader), Catch::Matchers::ContainsSubstring("Could not read complete binary data"));
        }
        for (const u8 flag : {2, 255})
        {
            resources::BinaryReader reader(files.Write("bad-flag.bin", ModelBytes({}, {flag})).string());
            CHECK_THROWS_WITH(Model(reader), Catch::Matchers::ContainsSubstring("Invalid packed model bounds flag"));
        }
        const auto nan = std::numeric_limits<f32>::quiet_NaN();
        const auto infinity = std::numeric_limits<f32>::infinity();
        for (const auto& [min, max] : std::vector<std::pair<Vector3, Vector3>>{
            {{2, 0, 0}, {1, 1, 1}}, {{0, 2, 0}, {1, 1, 1}}, {{0, 0, 2}, {1, 1, 1}},
            {{nan, 0, 0}, {1, 1, 1}}, {{0, 0, 0}, {1, infinity, 1}}, {{0, 0, -infinity}, {1, 1, 1}}})
        {
            std::vector<u8> bounds{1};
            AppendVector(bounds, min);
            AppendVector(bounds, max);
            resources::BinaryReader reader(files.Write("invalid-bounds.bin", ModelBytes({}, bounds)).string());
            CHECK_THROWS_WITH(Model(reader), Catch::Matchers::ContainsSubstring("Invalid packed model bounds"));
        }
    });
}
