#include "pch.h"
#include "support/AssetTestSupport.h"
#include "support/GeometryTestSupport.h"
#include "support/NativeImageTestSupport.h"
#include "Modules/Assets/Types/TextAsset.h"
#include "Modules/Physics/ModelCollider.h"
#include "Modules/Render/Model/Model.h"
#include "Modules/Resources/AssetBuilder.h"
#include "Modules/Resources/Builders/FontBuilder.h"
#include "Modules/Resources/Builders/ModelBuilder.h"
#include "Modules/Resources/Builders/TextureBuilder.h"
#include "Modules/Resources/Serialization/BinaryWriter.h"
#include "glad/glad.h"
#include "assimp/Exporter.hpp"
#include "assimp/cexport.h"
#include "assimp/cimport.h"
#include "assimp/postprocess.h"
#include <array>

using namespace rei;
using namespace rei::math;
using namespace rei::tests;

namespace
{
    const std::string TRIANGLE_OBJ = "o triangle\nv 0 0 5\nv 2 0 5\nv 0 2 5\nvt 0 0\nvt 1 0\nvt 0 1\nf 1/1 2/2 3/3\n";

    std::filesystem::path WriteText(TemporaryDirectory& files, const std::string& name, const std::string& text)
    {
        return files.Write(name, {text.begin(), text.end()});
    }

    void CheckPngPackage(const u32 channels, const i32 expectedFormat)
    {
        TemporaryDirectory files;
        const auto source = files.Write("pixels.png", PngBytes(channels));
        const auto target = files.Write("texture.bin", {});
        resources::BinaryWriter writer(target.string(), 0);
        resources::TextureBuilder().BuildTextureAsset(source, writer);
        writer.Close();
        resources::BinaryReader reader(target.string());
        CHECK(reader.GetI32() == 2);
        CHECK(reader.GetI32() == 2);
        CHECK(reader.GetI32() == expectedFormat);
        i32 length = 0;
        const std::unique_ptr<u8[]> bytes(reader.GetBytes(length));
        REQUIRE(length == static_cast<i32>(4 * channels));
        const auto sourcePixels = PngPixels(channels);
        std::vector<u8> flipped(sourcePixels.begin() + 2 * channels, sourcePixels.end());
        flipped.insert(flipped.end(), sourcePixels.begin(), sourcePixels.begin() + 2 * channels);
        CHECK(std::vector<u8>(bytes.get(), bytes.get() + length) == flipped);
        CHECK(reader.GetPosition() == 16 + length);
    }

    std::array<size_t, 3> MatchTrianglePoints(const render::Face& face, const std::array<Vector3, 3>& expected)
    {
        REQUIRE(face.Vertices.size() == 3);
        std::array<size_t, 3> matches{};
        std::array<bool, 3> found{};
        for (size_t vertexIndex = 0; vertexIndex < 3; ++vertexIndex)
        {
            const auto position = Vector3(face.Vertices[vertexIndex].Position);
            size_t expectedIndex = 3;
            for (size_t index = 0; index < 3; ++index)
            {
                const auto difference = position - expected[index];
                if (std::abs(difference.x) <= 1e-4f && std::abs(difference.y) <= 1e-4f && std::abs(difference.z) <= 1e-4f) expectedIndex = index;
            }
            CAPTURE(vertexIndex, position.x, position.y, position.z);
            REQUIRE(expectedIndex < 3);
            CHECK_FALSE(found[expectedIndex]);
            found[expectedIndex] = true;
            matches[vertexIndex] = expectedIndex;
        }
        for (const auto present : found) CHECK(present);
        return matches;
    }
    std::string FbxExportFormat()
    {
        Assimp::Exporter exporter;
        for (const std::string candidate : {"fbxa", "fbx"})
        {
            for (size_t index = 0; index < exporter.GetExportFormatCount(); ++index)
            {
                const auto description = exporter.GetExportFormatDescription(index);
                if (description && candidate == description->id) return candidate;
            }
        }
        return {};
    }

    std::filesystem::path ExportTriangleFbx(TemporaryDirectory& files, const bool transformed)
    {
        // Tests use debug CRT; bundled Assimp uses its own DLL heap. Import,
        // copy and free through Assimp so no test-owned new[] reaches DLL
        // aiScene/aiNode destructors. Coordinates and UV still come from an
        // independently specified source triangle rather than importer output.
        const std::unique_ptr<const aiScene, decltype(&aiReleaseImport)> imported(
            aiImportFileFromMemory(TRIANGLE_OBJ.data(), static_cast<u32>(TRIANGLE_OBJ.size()), aiProcess_Triangulate | aiProcess_GenNormals, "obj"), aiReleaseImport);
        INFO("Assimp fixture import: " << aiGetErrorString());
        REQUIRE(imported != nullptr);
        aiScene* copied = nullptr;
        aiCopyScene(imported.get(), &copied);
        const std::unique_ptr<aiScene, decltype(&aiFreeScene)> scene(copied, aiFreeScene);
        REQUIRE(scene != nullptr);
        REQUIRE(scene->mRootNode != nullptr);
        REQUIRE(scene->mNumMeshes == 1);
        REQUIRE(scene->mRootNode->mNumChildren == 1);
        auto node = scene->mRootNode->mChildren[0];
        REQUIRE(node != nullptr);
        REQUIRE(node->mNumMeshes == 1);
        if (transformed)
        {
            node->mTransformation.a1 = 2;
            node->mTransformation.b2 = 3;
            node->mTransformation.a4 = 10;
            node->mTransformation.b4 = -2;
            node->mTransformation.c4 = 3;
        }
        const auto source = files.File("triangle.fbx");
        Assimp::Exporter exporter;
        const auto format = FbxExportFormat();
        REQUIRE_FALSE(format.empty());
        const auto result = exporter.Export(scene.get(), format, source.string());
        INFO("Assimp FBX export: " << exporter.GetErrorString());
        REQUIRE(result == AI_SUCCESS);
        REQUIRE(std::filesystem::file_size(source) > 0);
        return source;
    }

    void CheckJpegPackage(const std::string& filename, const u32 channels, const i32 expectedFormat)
    {
        TemporaryDirectory files;
        const auto source = std::filesystem::path(__FILE__).parent_path() / "assets/importers" / filename;
        REQUIRE(std::filesystem::exists(source));
        const auto target = files.Write("jpeg.bin", {});
        resources::BinaryWriter writer(target.string(), 0);
        resources::TextureBuilder().BuildTextureAsset(source, writer);
        writer.Close();
        resources::BinaryReader reader(target.string());
        REQUIRE(reader.GetI32() == 8);
        REQUIRE(reader.GetI32() == 8);
        CHECK(reader.GetI32() == expectedFormat);
        i32 count = 0;
        const std::unique_ptr<u8[]> pixels(reader.GetBytes(count));
        REQUIRE(count == static_cast<i32>(64 * channels));
        for (u32 row = 0; row < 8; ++row)
        {
            const bool bottomSourceBand = row < 4; // Builder reverses rows.
            for (u32 column = 0; column < 8; ++column)
            {
                for (u32 channel = 0; channel < channels; ++channel)
                {
                    const u32 offset = (row * 8 + column) * channels + channel;
                    const std::array<i32, 3> rgb = bottomSourceBand ? std::array<i32, 3>{16, 32, 224} : std::array<i32, 3>{224, 32, 16};
                    const i32 expected = channels == 1 ? (bottomSourceBand ? 224 : 32) : rgb[channel];
                    CAPTURE(row, column, channel, expected);
                    // JPEG is lossy. Source bands are constant; quality 100,
                    // no chroma subsampling. Independent source oracle permits
                    // only small quantization differences, not wrong channels.
                    CHECK(std::abs(static_cast<i32>(pixels[offset]) - expected) <= 3);
                }
            }
        }
        CHECK(reader.GetPosition() == 16 + count);
    }

    std::filesystem::path ImportModel(TemporaryDirectory& files, const std::string& text)
    {
        const auto source = WriteText(files, "model.obj", text);
        const auto target = files.Write("model.bin", {});
        resources::BinaryWriter writer(target.string(), 0);
        ModelBuilder().BuildModelAsset(source, writer);
        writer.Close();
        return target;
    }
}

TEST_CASE("IMP-01 OBJ importer packages generated normals flipped UV and CPU BVH", "[native][importers][physics][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        TemporaryDirectory files;
        resources::BinaryReader reader(ImportModel(files, TRIANGLE_OBJ).string());
        render::Model model(reader);
        REQUIRE(model.GetMeshes().size() == 1);
        const auto& mesh = model.GetMeshes()[0];
        REQUIRE(mesh.Vertices.size() == 3);
        REQUIRE(mesh.Faces.size() == 1);
        REQUIRE(mesh.Faces[0].Vertices.size() == 3);
        CHECK(mesh.Indices == std::vector<u32>{0, 1, 2});
        for (const auto& vertex : mesh.Vertices) CheckVector(Vector3(vertex.Normal), {0, 0, 1});
        CHECK(mesh.Vertices[0].TexCoords.x == Catch::Approx(0));
        CHECK(mesh.Vertices[0].TexCoords.y == Catch::Approx(1));
        CHECK(mesh.Vertices[2].TexCoords.y == Catch::Approx(0));
        CHECK(mesh.VAO == 0);
        Vector3 point;
        REQUIRE(mesh.BVHRoot.IsRayIntersecting(Ray({0.5f, 0.5f, 0}, {0, 0, 1}), glm::mat4(1), point));
        CheckVector(point, {0.5f, 0.5f, 5});
    });
}

TEST_CASE("IMP-01 OBJ quad triangulates and absent UV defaults to zero", "[native][importers][physics][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        TemporaryDirectory files;
        resources::BinaryReader reader(ImportModel(files, "o quad\nv 0 0 5\nv 2 0 5\nv 2 2 5\nv 0 2 5\nf 1 2 3 4\n").string());
        render::Model model(reader);
        REQUIRE(model.GetMeshes().size() == 1);
        const auto& mesh = model.GetMeshes()[0];
        REQUIRE(mesh.Faces.size() == 2);
        REQUIRE(mesh.Indices.size() == 6);
        for (const auto& face : mesh.Faces) REQUIRE(face.Vertices.size() == 3);
        for (const auto& vertex : mesh.Vertices)
        {
            CHECK(vertex.TexCoords.x == 0);
            CHECK(vertex.TexCoords.y == 0);
        }
        Vector3 point;
        REQUIRE(mesh.BVHRoot.IsRayIntersecting(Ray({1, 1, 0}, {0, 0, 1}), glm::mat4(1), point));
        CheckVector(point, {1, 1, 5});
    });
}

TEST_CASE("IMP-02 Missing OBJ fails explicitly instead of dereferencing Assimp scene", "[native][importers][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        TemporaryDirectory files;
        resources::BinaryWriter writer(files.Write("failed.bin", {}).string(), 0);
        CHECK_THROWS(ModelBuilder().BuildModelAsset(files.File("missing.obj"), writer));
    });
}

TEST_CASE("IMP-02 Invalid OBJ fails explicitly with bounded import", "[native][importers][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        TemporaryDirectory files;
        const auto source = WriteText(files, "invalid.obj", "not a model\n");
        resources::BinaryWriter writer(files.Write("failed.bin", {}).string(), 0);
        CHECK_THROWS(ModelBuilder().BuildModelAsset(source, writer));
    });
}

TEST_CASE("IMP-03 Text builder preserves UTF8 embedded NUL and packed offsets", "[native][importers][binary][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        TemporaryDirectory files;
        std::string text = "hello \xD0\xBC\xD0\xB8\xD1\x80\n";
        text.push_back('\0');
        text += "tail";
        const auto source = WriteText(files, "text.asset", text);
        const auto target = files.Write("pack.bin", {0x11, 0x22, 0x33});
        const auto end = resources::AssetBuilder().BuildAsset(source.string(), target.string(), 3);
        CHECK(end == static_cast<i64>(3 + 4 + text.size()));
        const auto bytes = ReadBytes(target);
        REQUIRE(bytes.size() >= 3);
        CHECK(std::vector<u8>(bytes.begin(), bytes.begin() + 3) == std::vector<u8>{0x11, 0x22, 0x33});
        resources::BinaryReader reader(target.string(), 3);
        assets::TextAsset asset(reader);
        CHECK(asset.GetValue() == text);
        CHECK(reader.GetPosition() == end);
    });
}

TEST_CASE("IMP-03 UTF8 BOM is removed once without stripping interior BOM", "[native][importers][binary][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        TemporaryDirectory files;
        const std::string bom = "\xEF\xBB\xBF";
        const std::string body = "content" + bom + "tail";
        const auto source = WriteText(files, "bom.asset", bom + body);
        const auto target = files.File("bom.bin");
        REQUIRE(resources::AssetBuilder().BuildAsset(source.string(), target.string(), 0) > 0);
        resources::BinaryReader reader(target.string());
        CHECK(assets::TextAsset(reader).GetValue() == body);
    });
}

TEST_CASE("IMP-03 Empty and short text inputs produce exact length-prefixed strings", "[native][importers][binary][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        const std::string text = GENERATE(std::string{}, std::string{"x"}, std::string{"xy"});
        TemporaryDirectory files;
        const auto source = WriteText(files, "short.asset", text);
        const auto target = files.File("short.bin");
        CHECK(resources::AssetBuilder().BuildAsset(source.string(), target.string(), 0) == static_cast<i64>(4 + text.size()));
        resources::BinaryReader reader(target.string());
        CHECK(assets::TextAsset(reader).GetValue() == text);
    });
}

TEST_CASE("IMP-03 Missing text source returns failure and preserves existing destination", "[native][importers][binary][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        TemporaryDirectory files;
        const auto target = files.Write("existing.bin", {9, 8, 7});
        CHECK(resources::AssetBuilder().BuildAsset(files.File("missing.asset").string(), target.string(), 0) == 0);
        CHECK(ReadBytes(target) == std::vector<u8>{9, 8, 7});
    });
}

TEST_CASE("IMP-01 Asset extension matching imports uppercase OBJ as model", "[native][importers][binary][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        TemporaryDirectory files;
        const auto source = WriteText(files, "MODEL.OBJ", TRIANGLE_OBJ);
        const auto target = files.File("uppercase.bin");
        REQUIRE(resources::AssetBuilder().BuildAsset(source.string(), target.string(), 0) > 0);
        resources::BinaryReader reader(target.string());
        CHECK(reader.GetStr() == "MODEL.OBJ");
        // Check header first; parsing raw fallback text as a Model would turn
        // this routing assertion into an unrelated malformed-binary failure.
    });
}

TEST_CASE("IMP-04 Font builder copies bundled font bytes without format loss", "[native][importers][font][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        TemporaryDirectory files;
        const auto source = std::filesystem::path(__FILE__).parent_path().parent_path() / "resources/fonts/Roboto-Regular.ttf";
        const auto expected = ReadBytes(source);
        REQUIRE_FALSE(expected.empty());
        const auto target = files.Write("font.bin", {});
        resources::BinaryWriter writer(target.string(), 0);
        resources::FontBuilder().BuildFontAsset(source, writer);
        writer.Close();
        resources::BinaryReader reader(target.string());
        i32 count = 0;
        const std::unique_ptr<u8[]> bytes(reader.GetBytes(count));
        REQUIRE(count == static_cast<i32>(expected.size()));
        CHECK(std::vector<u8>(bytes.get(), bytes.get() + count) == expected);
        CHECK(reader.GetPosition() == static_cast<i64>(expected.size() + 4));
    });
}

TEST_CASE("IMP-04 Empty and missing fonts reject before writing payload", "[native][importers][font][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        TemporaryDirectory files;
        const auto target = files.Write("font.bin", {});
        const auto empty = files.Write("empty.ttf", {});
        resources::BinaryWriter writer(target.string(), 0);
        CHECK_THROWS(resources::FontBuilder().BuildFontAsset(empty, writer));
        CHECK_THROWS(resources::FontBuilder().BuildFontAsset(files.File("missing.ttf"), writer));
        CHECK(writer.GetPosition() == 0);
    });
}

TEST_CASE("IMP-05 Grayscale PNG uses red channel and flips row order", "[native][importers][texture][coverage][coverage-remaining][isolated]")
{
    Isolated([] { CheckPngPackage(1, GL_RED); });
}

TEST_CASE("IMP-05 Grayscale alpha PNG uses two channels and retains alpha", "[native][importers][texture][coverage][coverage-remaining][isolated]")
{
    Isolated([] { CheckPngPackage(2, GL_RG); });
}

TEST_CASE("IMP-05 RGB PNG uses three channels and flips row order", "[native][importers][texture][coverage][coverage-remaining][isolated]")
{
    Isolated([] { CheckPngPackage(3, GL_RGB); });
}

TEST_CASE("IMP-05 RGBA PNG uses four channels and retains alpha", "[native][importers][texture][coverage][coverage-remaining][isolated]")
{
    Isolated([] { CheckPngPackage(4, GL_RGBA); });
}

TEST_CASE("IMP-05 Missing and invalid PNG reject before writing package", "[native][importers][texture][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        TemporaryDirectory files;
        const auto target = files.Write("failed.bin", {});
        resources::BinaryWriter writer(target.string(), 0);
        const auto invalid = files.Write("invalid.png", {1, 2, 3});
        CHECK_THROWS(resources::TextureBuilder().BuildTextureAsset(invalid, writer));
        CHECK_THROWS(resources::TextureBuilder().BuildTextureAsset(files.File("missing.png"), writer));
        CHECK(writer.GetPosition() == 0);
    });
}

TEST_CASE("IMP-05 RGB JPEG preserves quantized channels and vertical row flip", "[native][importers][texture][coverage][coverage-remaining][isolated]")
{
    Isolated([] { CheckJpegPackage("rgb-bands.jpg", 3, GL_RGB); });
}

TEST_CASE("IMP-05 Grayscale JPEG preserves quantized red channel and row flip", "[native][importers][texture][coverage][coverage-remaining][isolated]")
{
    Isolated([] { CheckJpegPackage("gray-bands.jpg", 1, GL_RED); });
}

TEST_CASE("IMP-05 Invalid JPEG rejects before writing package", "[native][importers][texture][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        TemporaryDirectory files;
        const auto source = files.Write("invalid.jpg", {0xff, 0xd8, 0xff, 0x00, 0x11});
        resources::BinaryWriter writer(files.Write("failed.bin", {}).string(), 0);
        CHECK_THROWS(resources::TextureBuilder().BuildTextureAsset(source, writer));
        CHECK(writer.GetPosition() == 0);
    });
}

TEST_CASE("IMP-05 Missing JPEG rejects before writing package", "[native][importers][texture][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        TemporaryDirectory files;
        resources::BinaryWriter writer(files.Write("failed.bin", {}).string(), 0);
        CHECK_THROWS(resources::TextureBuilder().BuildTextureAsset(files.File("missing.jpg"), writer));
        CHECK(writer.GetPosition() == 0);
    });
}

TEST_CASE("IMP-01 FBX importer preserves triangle coordinates normals UV and packed BVH", "[native][importers][physics][coverage][coverage-remaining][isolated]")
{
    if (FbxExportFormat().empty()) SKIP("Prerequisite unavailable: bundled Assimp FBX exporter (fbxa or fbx)");
    Isolated([]
    {
        TemporaryDirectory files;
        const auto source = ExportTriangleFbx(files, false);
        const auto target = files.Write("fbx.bin", {});
        resources::BinaryWriter writer(target.string(), 0);
        ModelBuilder().BuildModelAsset(source, writer);
        writer.Close();
        resources::BinaryReader reader(target.string());
        render::Model model(reader);
        REQUIRE(model.GetMeshes().size() == 1);
        const auto& mesh = model.GetMeshes()[0];
        REQUIRE(mesh.Faces.size() == 1);
        REQUIRE(mesh.Faces[0].Vertices.size() == 3);
        const std::array<Vector3, 3> expected = {{{0, 0, 5}, {2, 0, 5}, {0, 2, 5}}};
        const auto matches = MatchTrianglePoints(mesh.Faces[0], expected);
        for (size_t index = 0; index < 3; ++index)
        {
            const auto& vertex = mesh.Faces[0].Vertices[index];
            CheckVector(Vector3(vertex.Normal), {0, 0, 1});
            CHECK(vertex.TexCoords.x == Catch::Approx(expected[matches[index]].x * 0.5f).margin(1e-4f));
            CHECK(vertex.TexCoords.y == Catch::Approx(1.0f - expected[matches[index]].y * 0.5f).margin(1e-4f));
        }
        Vector3 hit;
        REQUIRE(mesh.BVHRoot.IsRayIntersecting(Ray({0.5f, 0.5f, 0}, {0, 0, 1}), glm::mat4(1), hit));
        CheckVector(hit, {0.5f, 0.5f, 5});
        CHECK(reader.GetPosition() == static_cast<i64>(std::filesystem::file_size(target)));
    });
}

TEST_CASE("IMP-01 FBX node transforms reach packed geometry and collision surface", "[native][importers][physics][coverage][coverage-remaining][isolated]")
{
    if (FbxExportFormat().empty()) SKIP("Prerequisite unavailable: bundled Assimp FBX exporter (fbxa or fbx)");
    Isolated([]
    {
        TemporaryDirectory files;
        const auto source = ExportTriangleFbx(files, true);
        const auto target = files.Write("transformed-fbx.bin", {});
        resources::BinaryWriter writer(target.string(), 0);
        ModelBuilder().BuildModelAsset(source, writer);
        writer.Close();
        resources::BinaryReader reader(target.string());
        render::Model model(reader);
        REQUIRE(model.GetMeshes().size() == 1);
        const auto& mesh = model.GetMeshes()[0];
        REQUIRE(mesh.Faces.size() == 1);
        REQUIRE(mesh.Faces[0].Vertices.size() == 3);
        const std::array<Vector3, 3> expected = {{{10, -2, 8}, {14, -2, 8}, {10, 4, 8}}};
        static_cast<void>(MatchTrianglePoints(mesh.Faces[0], expected));
        Vector3 hit;
        REQUIRE(mesh.BVHRoot.IsRayIntersecting(Ray({11, -1, 0}, {0, 0, 1}), glm::mat4(1), hit));
        CheckVector(hit, {11, -1, 8});
    });
}

TEST_CASE("IMP-02 Invalid FBX fails explicitly within bounded child", "[native][importers][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        TemporaryDirectory files;
        const auto source = WriteText(files, "invalid.fbx", "not an FBX document\n");
        resources::BinaryWriter writer(files.Write("failed.bin", {}).string(), 0);
        CHECK_THROWS(ModelBuilder().BuildModelAsset(source, writer));
        CHECK(writer.GetPosition() == 0);
    });
}
