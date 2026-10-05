#include "pch.h"
#include "support/NativeTestSupport.h"
#include "Common/Logging/Logger.h"
#include <atomic>
#include <thread>

using namespace rei::tests;
using namespace rei::common::logging;

TEST_CASE("Logger disabled and threshold suppression affects events and snapshots", "[native][coverage][coverage-remaining][logging][isolated]")
{
    Isolated([]
    {
        Logger logger("Coverage");
        std::vector<std::string> events;
        logger.NewLogEvent.append([&](const auto& message) { events.emplace_back(message.Message); });
        const auto before = GetRecentLogEntriesSnapshot();
        logger.SetMinLogLevel(Warning);
        CHECK(logger.GetMinLogLevel() == Warning);
        logger.Log(Debug, "filtered-debug");
        logger.Log(Info, "filtered-info");
        logger.Disable();
        logger.Log(Error, "disabled-error");
        CHECK(events.empty());
        CHECK(GetRecentLogEntriesSnapshot() == before);
        logger.Enable();
        logger.Log(Warning, "accepted-warning", "details");
        REQUIRE(events == std::vector<std::string>{"accepted-warning"});
        CHECK(GetRecentLogEntriesSnapshot().back() == "WARN | accepted-warning | details");
    });
}

TEST_CASE("Logger event carries configured scope and independent message details", "[native][coverage][coverage-remaining][logging][isolated]")
{
    Isolated([]
    {
        Logger logger("SceneCoverage");
        std::string scope, text, details;
        LogLevelEnum level = Debug;
        logger.NewLogEvent.append([&](const auto& message)
        {
            scope = message.Scope;
            text = message.Message;
            details = message.Details;
            level = message.Level;
        });
        logger.Log(Warning, "UTF8 \xD0\xA0\xD0\xB5\xD0\xB9", "line1\nline2");
        CHECK(scope == "SceneCoverage");
        CHECK(text == "UTF8 \xD0\xA0\xD0\xB5\xD0\xB9");
        CHECK(details == "line1\nline2");
        CHECK(level == Warning);
    });
}

TEST_CASE("Logger recent snapshots evict oldest entries at capacity 256", "[native][coverage][coverage-remaining][logging][isolated]")
{
    Isolated([]
    {
        Logger logger("Coverage");
        for (i32 index = 0; index < 260; ++index) logger.Log(Info, "entry-" + std::to_string(index));
        const auto snapshot = GetRecentLogEntriesSnapshot();
        REQUIRE(snapshot.size() == 256);
        CHECK(snapshot.front() == "INFO | entry-4");
        CHECK(snapshot.back() == "INFO | entry-259");
        logger.Log(Info, "new-entry");
        CHECK(snapshot.front() == "INFO | entry-4");
        CHECK(snapshot.back() == "INFO | entry-259");
        CHECK(GetRecentLogEntriesSnapshot().front() == "INFO | entry-5");
    });
}

TEST_CASE("Logger concurrent snapshots contain complete bounded records", "[native][coverage][coverage-remaining][logging][isolated]")
{
    Isolated([]
    {
        Logger logger("Coverage");
        std::atomic<bool> finished = false, valid = true;
        std::atomic<i32> events = 0;
        logger.NewLogEvent.append([&](const auto&) { ++events; });
        std::thread reader([&]
        {
            while (!finished)
            {
                const auto snapshot = GetRecentLogEntriesSnapshot();
                if (snapshot.size() > 256) valid = false;
                for (const auto& record : snapshot)
                    if (!record.starts_with("INFO | writer-") || !record.ends_with(" | tail")) valid = false;
            }
        });
        std::vector<std::thread> writers;
        for (i32 writer = 0; writer < 4; ++writer)
            writers.emplace_back([&, writer]
            {
                for (i32 index = 0; index < 80; ++index)
                    logger.Log(Info, "writer-" + std::to_string(writer) + ":" + std::to_string(index), "tail");
            });
        for (auto& writer : writers) writer.join();
        finished = true;
        reader.join();
        CHECK(valid.load());
        CHECK(events == 320);
        CHECK(GetRecentLogEntriesSnapshot().size() == 256);
    });
}

TEST_CASE("Logger subscriber can log and read snapshots reentrantly", "[native][coverage][coverage-remaining][logging][isolated]")
{
    Isolated([]
    {
        Logger logger("Coverage");
        std::vector<std::string> events;
        logger.NewLogEvent.append([&](const auto& message)
        {
            events.emplace_back(message.Message);
            REQUIRE_FALSE(GetRecentLogEntriesSnapshot().empty());
            if (std::string(message.Message) == "outer") logger.Log(Info, "inner");
        });
        logger.Log(Info, "outer");
        CHECK(events == std::vector<std::string>{"outer", "inner"});
        const auto snapshot = GetRecentLogEntriesSnapshot();
        REQUIRE(snapshot.size() >= 2);
        CHECK(snapshot[snapshot.size() - 2] == "INFO | outer");
        CHECK(snapshot.back() == "INFO | inner");
    });
}
