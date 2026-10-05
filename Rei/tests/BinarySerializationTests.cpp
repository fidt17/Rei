#include "pch.h"
#include "support/NativeTestSupport.h"
#include "Modules/Resources/Serialization/BinaryReader.h"
#include "Modules/Resources/Serialization/BinaryWriter.h"
#include <new>
#include <stdexcept>

using rei::resources::BinaryReader;
using rei::resources::BinaryWriter;
using namespace rei::tests;

namespace
{
    void CheckRejectedWithoutAllocatorException(const std::string& operation, const std::function<void()>& action)
    {
        INFO(operation);
        bool rejected = false;
        try { action(); }
        catch (const std::bad_alloc&) { FAIL_CHECK("Corrupt input reached allocation instead of validation"); }
        catch (const std::length_error&) { FAIL_CHECK("Container size exception is not input validation"); }
        catch (const std::exception&) { rejected = true; }
        CHECK(rejected);
    }

    const std::vector<u8> SCALAR_BYTES{
        0xAB, 0x34, 0x12, 0xEF, 0xCD, 0xAB, 0x89,
        0xEF, 0xCD, 0xAB, 0x89, 0x67, 0x45, 0x23, 0x01,
        0xFE, 0xFE, 0xFF, 0xFE, 0xFF, 0xFF, 0xFF,
        0xFE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        0x00, 0x00, 0xC0, 0x3F
    };
}

TEST_CASE("BIN-01 Writer scalars match Windows x64 golden bytes", "[native][binary][coverage]")
{
    TemporaryDirectory directory;
    const auto path = directory.Write("scalars.bin", {});
    BinaryWriter writer(path.string(), 0);
    writer.WriteU8(0xAB);
    writer.WriteU16(0x1234);
    writer.WriteU32(0x89ABCDEF);
    writer.WriteU64(0x0123456789ABCDEF);
    writer.WriteI8(-2);
    writer.WriteI16(-2);
    writer.WriteI32(-2);
    writer.WriteI64(-2);
    writer.WriteF32(1.5f);
    REQUIRE(writer.GetPosition() == 34);
    writer.Close();
    REQUIRE(ReadBytes(path) == SCALAR_BYTES);
}

TEST_CASE("BIN-01 Reader scalars decode independently prepared bytes", "[native][binary][coverage]")
{
    TemporaryDirectory directory;
    const auto path = directory.Write("scalars.bin", SCALAR_BYTES);
    BinaryReader reader(path.string());
    CHECK(reader.GetU8() == 0xAB);
    CHECK(reader.GetU16() == 0x1234);
    CHECK(reader.GetU32() == 0x89ABCDEF);
    CHECK(reader.GetU64() == 0x0123456789ABCDEF);
    CHECK(reader.GetI8() == -2);
    CHECK(reader.GetI16() == -2);
    CHECK(reader.GetI32() == -2);
    CHECK(reader.GetI64() == -2);
    CHECK(reader.GetF32() == 1.5f);
    REQUIRE(reader.GetPosition() == 34);
}

TEST_CASE("BIN-01 Writer preserves empty UTF8 and embedded NUL strings", "[native][binary][coverage]")
{
    TemporaryDirectory directory;
    const auto path = directory.Write("strings.bin", {});
    BinaryWriter writer(path.string(), 0);
    writer.WriteStr("");
    writer.WriteStr(std::string("a\0b", 3));
    writer.WriteStr("\xD0\xAF");
    writer.Close();
    const std::vector<u8> expected{0, 0, 0, 0, 3, 0, 0, 0, 'a', 0, 'b', 2, 0, 0, 0, 0xD0, 0xAF};
    REQUIRE(ReadBytes(path) == expected);
}

TEST_CASE("BIN-01 Reader preserves empty UTF8 and embedded NUL strings", "[native][binary][coverage]")
{
    TemporaryDirectory directory;
    const auto path = directory.Write("strings.bin", {0, 0, 0, 0, 3, 0, 0, 0, 'a', 0, 'b', 2, 0, 0, 0, 0xD0, 0xAF});
    BinaryReader reader(path.string());
    REQUIRE(reader.GetStr().empty());
    REQUIRE(reader.GetStr() == std::string("a\0b", 3));
    REQUIRE(reader.GetStr() == "\xD0\xAF");
    REQUIRE(reader.GetPosition() == 17);
}

TEST_CASE("BIN-01 Length-prefixed raw bytes keep payload and ownership", "[native][binary][coverage]")
{
    TemporaryDirectory directory;
    const auto path = directory.Write("bytes.bin", {});
    const std::vector<u8> payload{0, 0xFF, 0x80, 7};
    BinaryWriter writer(path.string(), 0);
    writer.WriteBytes(payload.data(), static_cast<i32>(payload.size()));
    writer.Close();
    REQUIRE(ReadBytes(path) == std::vector<u8>{4, 0, 0, 0, 0, 0xFF, 0x80, 7});

    const auto independent = directory.Write("independent.bin", {4, 0, 0, 0, 0, 0xFF, 0x80, 7, 0, 0, 0, 0, 0xAB});
    BinaryReader reader(independent.string());
    i32 count = -1;
    const std::unique_ptr<u8[]> bytes(reader.GetBytes(count));
    REQUIRE(count == 4);
    CHECK(std::vector<u8>(bytes.get(), bytes.get() + count) == payload);
    CHECK(reader.GetPosition() == 8);
    const std::unique_ptr<u8[]> emptyBytes(reader.GetBytes(count));
    CHECK(count == 0);
    CHECK(reader.GetPosition() == 12);
    CHECK(reader.GetU8() == 0xAB);
}

TEST_CASE("BIN-01 Reader vectors consume only their packed record", "[native][binary][coverage]")
{
    TemporaryDirectory directory;
    const auto path = directory.Write("vectors.bin", {
        3, 0, 0, 0, 0xF9, 0xFF, 0xFF, 0xFF, 0, 0, 0, 0, 9, 0, 0, 0,
        0, 0, 0, 0, 0xAB
    });
    BinaryReader reader(path.string());
    REQUIRE(reader.GetVector<i32>() == std::vector<i32>{-7, 0, 9});
    REQUIRE(reader.GetPosition() == 16);
    REQUIRE(reader.GetVector<i32>().empty());
    REQUIRE(reader.GetU8() == 0xAB);
    REQUIRE(reader.GetPosition() == 21);
}

TEST_CASE("BIN-01 Packed seek overwrites only requested bytes", "[native][binary][coverage]")
{
    TemporaryDirectory directory;
    const auto path = directory.Write("packed.bin", std::vector<u8>(16, 0xCC));
    BinaryWriter writer(path.string(), 4);
    writer.WriteU32(0x12345678);
    CHECK(writer.GetPosition() == 8);
    writer.SetPosition(12);
    writer.WriteU16(0xABCD);
    writer.Close();
    REQUIRE(ReadBytes(path) == std::vector<u8>{0xCC, 0xCC, 0xCC, 0xCC, 0x78, 0x56, 0x34, 0x12, 0xCC, 0xCC, 0xCC, 0xCC, 0xCD, 0xAB, 0xCC, 0xCC});
    BinaryReader reader(path.string(), 4);
    CHECK(reader.GetU32() == 0x12345678);
    reader.SetPosition(12);
    CHECK(reader.GetU16() == 0xABCD);
    reader.SetPosition(4);
    CHECK(reader.GetU32() == 0x12345678);
}

TEST_CASE("BIN-02 Reader rejects a nonexistent file", "[native][binary][coverage]")
{
    TemporaryDirectory directory;
    CHECK_THROWS_AS(BinaryReader(directory.File("missing.bin").string()), std::runtime_error);
}

TEST_CASE("BIN-02 Writer rejects an unavailable destination", "[native][binary][coverage]")
{
    TemporaryDirectory directory;
    CHECK_THROWS_AS(BinaryWriter((directory.File("missing") / "file.bin").string(), 0), std::runtime_error);
}

TEST_CASE("BIN-02 Reader rejects a truncated string payload", "[native][binary][coverage]")
{
    TemporaryDirectory directory;
    const u32 payloadBytes = GENERATE(0u, 1u, 2u, 3u);
    CAPTURE(payloadBytes);
    std::vector<u8> bytes{4, 0, 0, 0};
    bytes.insert(bytes.end(), payloadBytes, 'a');
    BinaryReader reader(directory.Write("truncated.bin", bytes).string());
    CHECK_THROWS_AS(reader.GetStr(), std::runtime_error);
}

TEST_CASE("BIN-02 Reader rejects a truncated scalar", "[native][binary][coverage][isolated]")
{
    Isolated([]
    {
        TemporaryDirectory directory;
        const auto checkTruncated = [&](auto value)
        {
            using T = decltype(value);
            for (u32 availableBytes = 0; availableBytes < sizeof(T); ++availableBytes)
            {
                CAPTURE(sizeof(T), availableBytes);
                BinaryReader reader(directory.Write("truncated.bin", std::vector<u8>(availableBytes, 0x42)).string());
                CHECK_THROWS_AS(reader.GetByType<T>(), std::runtime_error);
            }
        };
        checkTruncated(u8{});
        checkTruncated(u16{});
        checkTruncated(u32{});
        checkTruncated(u64{});
        checkTruncated(i8{});
        checkTruncated(i16{});
        checkTruncated(i32{});
        checkTruncated(i64{});
        checkTruncated(f32{});

        BinaryReader closedReader(directory.Write("closed-reader.bin", {0x42}).string());
        closedReader.Close();
        CHECK_THROWS_AS(closedReader.GetU8(), std::runtime_error);
    });
}

TEST_CASE("BIN-02 Reader rejects a truncated vector payload", "[native][binary][coverage][isolated]")
{
    Isolated([]
    {
        TemporaryDirectory directory;
        const u32 payloadBytes = GENERATE(0u, 1u, 2u, 3u, 4u, 5u, 6u, 7u, 8u, 9u, 10u, 11u);
        CAPTURE(payloadBytes);
        std::vector<u8> bytes{3, 0, 0, 0};
        bytes.insert(bytes.end(), payloadBytes, 0x42);
        BinaryReader reader(directory.Write("truncated.bin", bytes).string());
        CHECK_THROWS_AS(reader.GetVector<i32>(), std::runtime_error);
    });
}

TEST_CASE("BIN-03 Negative lengths are rejected in an isolated process", "[native][binary][coverage][isolated]")
{
    Isolated([]
    {
        TemporaryDirectory directory;
        const auto path = directory.Write("negative.bin", {0xFF, 0xFF, 0xFF, 0xFF});
        BinaryReader reader(path.string());
        CheckRejectedWithoutAllocatorException("GetVector<i32>", [&] { static_cast<void>(reader.GetVector<i32>()); });
        BinaryReader stringReader(path.string());
        CheckRejectedWithoutAllocatorException("GetStr", [&] { static_cast<void>(stringReader.GetStr()); });
        BinaryReader bytesReader(path.string());
        CheckRejectedWithoutAllocatorException("GetBytes", [&]
        {
            i32 count = 0;
            const std::unique_ptr<u8[]> bytes(bytesReader.GetBytes(count));
        });
    });
}

TEST_CASE("BIN-03 Declared payload larger than file fails before huge allocation", "[native][binary][coverage][isolated]")
{
    Isolated([]
    {
        TemporaryDirectory directory;
        const auto path = directory.Write("oversized.bin", {0, 0, 0, 0x40});
        BinaryReader reader(path.string());
        CheckRejectedWithoutAllocatorException("GetVector<i32>", [&] { static_cast<void>(reader.GetVector<i32>()); });
        BinaryReader stringReader(path.string());
        CheckRejectedWithoutAllocatorException("GetStr", [&] { static_cast<void>(stringReader.GetStr()); });
        BinaryReader bytesReader(path.string());
        CheckRejectedWithoutAllocatorException("GetBytes", [&]
        {
            i32 count = 0;
            const std::unique_ptr<u8[]> bytes(bytesReader.GetBytes(count));
        });
    });
}

TEST_CASE("BIN-02 Writer rejects writes after close", "[native][binary][coverage]")
{
    TemporaryDirectory directory;
    const auto path = directory.Write("closed.bin", {});
    BinaryWriter writer(path.string(), 0);
    writer.Close();
    CHECK_NOTHROW(writer.Close());
    CHECK_THROWS_AS(writer.WriteU32(7), std::runtime_error);
    CHECK_THROWS_AS(writer.WriteStr("payload"), std::runtime_error);
    const std::vector<u8> payload{1, 2, 3};
    CHECK_THROWS_AS(writer.WriteBytes(payload.data(), static_cast<i32>(payload.size())), std::runtime_error);
    CHECK(ReadBytes(path).empty());
}

TEST_CASE("BIN-02 Truncated length headers reject every length-prefixed reader", "[native][binary][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        const u32 headerBytes = GENERATE(0u, 1u, 2u, 3u);
        CAPTURE(headerBytes);
        TemporaryDirectory directory;
        const auto path = directory.Write("partial-length.bin", std::vector<u8>(headerBytes, 0));
        BinaryReader stringReader(path.string());
        CheckRejectedWithoutAllocatorException("GetStr", [&] { static_cast<void>(stringReader.GetStr()); });
        CHECK_THROWS_AS(stringReader.GetU8(), std::runtime_error); // Failed header cannot return a later scalar.
        BinaryReader vectorReader(path.string());
        CheckRejectedWithoutAllocatorException("GetVector<i32>", [&] { static_cast<void>(vectorReader.GetVector<i32>()); });
        BinaryReader bytesReader(path.string());
        CheckRejectedWithoutAllocatorException("GetBytes", [&]
        {
            i32 count = 0;
            const std::unique_ptr<u8[]> bytes(bytesReader.GetBytes(count));
        });
    });
}

TEST_CASE("BIN-02 Truncated raw byte payload rejects incomplete data", "[native][binary][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        TemporaryDirectory directory;
        const u32 payloadBytes = GENERATE(0u, 1u, 2u, 3u);
        CAPTURE(payloadBytes);
        std::vector<u8> bytes{4, 0, 0, 0};
        bytes.insert(bytes.end(), payloadBytes, 0xAB);
        BinaryReader reader(directory.Write("partial-bytes.bin", bytes).string());
        CheckRejectedWithoutAllocatorException("GetBytes", [&]
        {
            i32 count = 0;
            const std::unique_ptr<u8[]> bytes(reader.GetBytes(count));
        });
    });
}

TEST_CASE("BIN-02 Proposed negative reader seek rejects without poisoning valid cursor", "[native][binary][coverage][coverage-remaining][proposed-contract][isolated]")
{
    Isolated([]
    {
        TemporaryDirectory directory;
        BinaryReader reader(directory.Write("reader-seek.bin", {0xAB, 0xCD}).string());
        REQUIRE(reader.GetU8() == 0xAB);
        // Proposed seek error policy: explicit rejection leaves previous cursor
        // usable. Native API currently declares no atomic seek-failure policy.
        CheckRejectedWithoutAllocatorException("BinaryReader::SetPosition(-1)", [&] { reader.SetPosition(-1); });
        CHECK(reader.GetPosition() == 1);
        CHECK(reader.GetU8() == 0xCD);
    });
}

TEST_CASE("BIN-02 Proposed negative writer seek rejects without poisoning valid cursor", "[native][binary][coverage][coverage-remaining][proposed-contract][isolated]")
{
    Isolated([]
    {
        TemporaryDirectory directory;
        const auto path = directory.Write("writer-seek.bin", {0x11, 0x22, 0x33});
        BinaryWriter writer(path.string(), 1);
        CheckRejectedWithoutAllocatorException("BinaryWriter::SetPosition(-1)", [&] { writer.SetPosition(-1); });
        CHECK(writer.GetPosition() == 1);
        writer.WriteU8(0xAB);
        writer.Close();
        CHECK(ReadBytes(path) == std::vector<u8>{0x11, 0xAB, 0x33});
    });
}

TEST_CASE("BIN-02 Proposed negative constructor offsets reject before accepting stream", "[native][binary][coverage][coverage-remaining][proposed-contract][isolated]")
{
    Isolated([]
    {
        TemporaryDirectory directory;
        const auto path = directory.Write("negative-position.bin", {0x11});
        CheckRejectedWithoutAllocatorException("BinaryReader negative constructor offset", [&] { BinaryReader reader(path.string(), -1); });
        CheckRejectedWithoutAllocatorException("BinaryWriter negative constructor offset", [&] { BinaryWriter writer(path.string(), -1); });
        CHECK(ReadBytes(path) == std::vector<u8>{0x11});
    });
}

TEST_CASE("BIN-02 Writer reports byte range lock failure after successful open", "[native][binary][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        TemporaryDirectory directory;
        const auto path = directory.Write("locked-output.bin", {0x11, 0x22, 0x33});
        const u32 payloadSize = GENERATE(1u, 128u * 1024u);
        CAPTURE(payloadSize);
        BinaryWriter writer(path.string(), 0);
        REQUIRE(writer.GetPosition() == 0); // Open succeeded before lock injection.
        WinHandle locker(CreateFileW(path.c_str(), GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
            nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr));
        constexpr u32 LOCK_BYTES = 256 * 1024;
        OVERLAPPED lock{};
        REQUIRE(LockFileEx(locker.Get(), LOCKFILE_EXCLUSIVE_LOCK | LOCKFILE_FAIL_IMMEDIATELY, 0, LOCK_BYTES, 0, &lock));
        WinHandle independentWriter(CreateFileW(path.c_str(), GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
            nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr));
        const u8 probe = 0xFF;
        DWORD written = 0;
        const bool probeSucceeded = WriteFile(independentWriter.Get(), &probe, 1, &written, nullptr) != 0;
        const auto probeError = probeSucceeded ? ERROR_SUCCESS : GetLastError();
        REQUIRE_FALSE(probeSucceeded);
        REQUIRE(probeError == ERROR_LOCK_VIOLATION); // Proves real OS write failure.
        const std::vector<u8> payload(payloadSize, 0xAA); // Exercise buffered and large writes.
        CheckRejectedWithoutAllocatorException("WriteBytes or Close after OS write failure", [&]
        {
            writer.WriteBytes(payload.data(), static_cast<i32>(payload.size()));
            writer.Close(); // Buffered failure may become observable only here.
        });
        // Close while still locked even if WriteBytes threw, so teardown cannot
        // flush buffered data later after the fault condition has been removed.
        try { writer.Close(); }
        catch (const std::exception&) {}
        CHECK_NOTHROW(writer.Close()); // Failed flush still closed the file.
        CHECK_THROWS_AS(writer.WriteU8(0xFF), std::runtime_error);
        REQUIRE(UnlockFileEx(locker.Get(), 0, LOCK_BYTES, 0, &lock));
        CHECK(ReadBytes(path) == std::vector<u8>{0x11, 0x22, 0x33});
    });
}
