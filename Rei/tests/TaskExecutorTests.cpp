#include "pch.h"
#include "support/NativeTestSupport.h"
#include "Common/Tasks/TaskExecutor.h"
#include <array>
#include <atomic>
#include <chrono>
#include <future>
#include <set>
#include <thread>

using rei::Task;
using rei::TaskExecutor;
using rei::tests::Isolated;

TEST_CASE("TASK-01 FIFO drain includes tasks enqueued by callbacks", "[native][tasks][coverage][isolated]")
{
    Isolated([]
    {
        TaskExecutor executor;
        std::vector<i32> trace;
        auto first = std::make_shared<Task>([&]
        {
            trace.push_back(1);
            auto nested = std::make_shared<Task>([&] { trace.push_back(3); });
            executor.AddTask(nested);
        });
        auto second = std::make_shared<Task>([&] { trace.push_back(2); });
        executor.AddTask(first);
        executor.AddTask(second);
        executor.CompleteTasks();
        REQUIRE(trace == std::vector<i32>{1, 2, 3});
        first->WaitForCompletion();
        second->WaitForCompletion();
        executor.CompleteTasks();
        REQUIRE(trace.size() == 3);
    });
}

TEST_CASE("TASK-02 Completion releases multiple waiting callers", "[native][tasks][coverage][isolated]")
{
    Isolated([]
    {
        std::atomic<i32> returned = 0;
        std::atomic<i32> started = 0;
        i32 result = 0;
        Task task([&] { result = 42; });
        std::vector<std::thread> waiters;
        for (i32 i = 0; i < 4; ++i)
        {
            waiters.emplace_back([&]
            {
                ++started;
                task.WaitForCompletion();
                if (result == 42) ++returned;
            });
        }
        while (started != 4) std::this_thread::yield();
        task.Invoke();
        for (auto& waiter : waiters) waiter.join();
        REQUIRE(returned == 4);
        task.WaitForCompletion();
    });
}

TEST_CASE("TASK-02 Throwing action still completes every waiter", "[native][tasks][coverage][isolated]")
{
    Isolated([]
    {
        Task task([] { throw std::runtime_error("task action failed"); });
        std::promise<void> ready;
        std::atomic<bool> completed = false;
        std::thread waiter([&]
        {
            ready.set_value();
            task.WaitForCompletion();
            completed = true;
        });
        ready.get_future().wait();
        bool executorSawError = false;
        try { task.Invoke(); }
        catch (const std::runtime_error& error) { executorSawError = std::string(error.what()) == "task action failed"; }
        waiter.join();
        REQUIRE(executorSawError);
        REQUIRE(completed);
        task.WaitForCompletion();
    });
}

TEST_CASE("TASK-03 Failed drain preserves unexecuted tasks for next drain", "[native][tasks][coverage][isolated]")
{
    Isolated([]
    {
        TaskExecutor executor;
        std::vector<i32> trace;
        auto failure = std::make_shared<Task>([&] { trace.push_back(1); throw std::runtime_error("failed"); });
        auto next = std::make_shared<Task>([&] { trace.push_back(2); });
        auto last = std::make_shared<Task>([&] { trace.push_back(3); });
        executor.AddTask(failure);
        executor.AddTask(next);
        executor.AddTask(last);
        CHECK_THROWS_AS(executor.CompleteTasks(), std::runtime_error);
        REQUIRE(trace == std::vector<i32>{1});
        failure->WaitForCompletion();
        executor.CompleteTasks();
        CHECK(trace == std::vector<i32>{1, 2, 3});
        next->WaitForCompletion();
        last->WaitForCompletion();
    });
}

TEST_CASE("TASK-01 Concurrent producers deliver each task to one consumer", "[native][tasks][coverage][isolated]")
{
    Isolated([]
    {
        TaskExecutor executor;
        std::vector<std::pair<i32, i32>> trace;
        std::vector<std::thread> producers;
        for (i32 producer = 0; producer < 4; ++producer)
        {
            producers.emplace_back([&, producer]
            {
                for (i32 sequence = 0; sequence < 50; ++sequence)
                {
                    auto task = std::make_shared<Task>([&, producer, sequence] { trace.emplace_back(producer, sequence); });
                    executor.AddTask(task);
                }
            });
        }
        for (auto& producer : producers) producer.join();
        executor.CompleteTasks();
        REQUIRE(trace.size() == 200);
        const std::set<std::pair<i32, i32>> unique(trace.begin(), trace.end());
        REQUIRE(unique.size() == 200);
        for (i32 producer = 0; producer < 4; ++producer)
        {
            i32 expectedSequence = 0;
            for (const auto& item : trace)
            {
                if (item.first == producer) CHECK(item.second == expectedSequence++);
            }
            CHECK(expectedSequence == 50);
        }
    });
}

TEST_CASE("TASK-01 Concurrent producers enqueue during active drain without loss or duplication", "[native][tasks][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        constexpr i32 PRODUCERS = 4;
        constexpr i32 TASKS_PER_PRODUCER = 50;
        TaskExecutor executor;
        std::vector<std::pair<i32, i32>> trace; // Written only by consumer thread.
        std::array<std::promise<void>, PRODUCERS> batchesEnqueued;
        std::array<std::promise<void>, PRODUCERS> initialTasksEnqueued;
        std::array<std::shared_future<void>, PRODUCERS> batchSignals;
        std::array<std::future<void>, PRODUCERS> initialSignals;
        std::atomic<bool> timedOut = false;
        std::atomic<i32> enqueuedDuringCallback = 0;
        std::atomic<bool> batchReleaseSent = false;
        std::promise<void> anyCallbackEntered;
        const auto batchGate = anyCallbackEntered.get_future().share();
        std::promise<void> drainEntered;
        auto entered = drainEntered.get_future();
        std::promise<void> allowFirstCallback;
        const auto firstCallbackRelease = allowFirstCallback.get_future().share();
        auto first = std::make_shared<Task>([&]
        {
            drainEntered.set_value();
            if (firstCallbackRelease.wait_for(std::chrono::seconds(2)) != std::future_status::ready) timedOut = true;
        });
        executor.AddTask(first);
        for (i32 producer = 0; producer < PRODUCERS; ++producer)
        {
            batchSignals[producer] = batchesEnqueued[producer].get_future().share();
            initialSignals[producer] = initialTasksEnqueued[producer].get_future();
        }
        std::exception_ptr drainError;
        std::thread consumer([&]
        {
            try { executor.CompleteTasks(); }
            catch (...) { drainError = std::current_exception(); }
        });
        // No Catch assertions while joinable threads exist: all waits have
        // deadlines, gates are released and threads joined before reporting.
        if (entered.wait_for(std::chrono::seconds(2)) != std::future_status::ready) timedOut = true;
        std::vector<std::thread> producers;
        for (i32 producer = 0; producer < PRODUCERS; ++producer)
        {
            producers.emplace_back([&, producer]
            {
                auto gate = std::make_shared<Task>([&]
                {
                    if (!batchReleaseSent.exchange(true)) anyCallbackEntered.set_value();
                    // All four producers enqueue concurrently while first gate
                    // runs inside CompleteTasks. Queue lock must stay available.
                    const auto batchDeadline = std::chrono::steady_clock::now() + std::chrono::seconds(2);
                    for (const auto& signal : batchSignals)
                    {
                        if (signal.wait_until(batchDeadline) != std::future_status::ready) timedOut = true;
                    }
                });
                executor.AddTask(gate);
                initialTasksEnqueued[producer].set_value();
                if (batchGate.wait_for(std::chrono::seconds(2)) != std::future_status::ready)
                {
                    timedOut = true;
                    batchesEnqueued[producer].set_value();
                    return;
                }
                for (i32 sequence = 0; sequence < TASKS_PER_PRODUCER; ++sequence)
                {
                    auto task = std::make_shared<Task>([&, producer, sequence] { trace.emplace_back(producer, sequence); });
                    executor.AddTask(task);
                    ++enqueuedDuringCallback;
                }
                batchesEnqueued[producer].set_value();
            });
        }
        const auto initialDeadline = std::chrono::steady_clock::now() + std::chrono::seconds(2);
        for (auto& signal : initialSignals)
        {
            if (signal.wait_until(initialDeadline) != std::future_status::ready) timedOut = true;
        }
        allowFirstCallback.set_value();
        for (auto& producer : producers) producer.join();
        consumer.join();
        REQUIRE_FALSE(timedOut.load());
        REQUIRE(drainError == nullptr);
        CHECK(enqueuedDuringCallback == PRODUCERS * TASKS_PER_PRODUCER);
        REQUIRE(trace.size() == PRODUCERS * TASKS_PER_PRODUCER);
        const std::set<std::pair<i32, i32>> unique(trace.begin(), trace.end());
        CHECK(unique.size() == trace.size());
        for (i32 producer = 0; producer < PRODUCERS; ++producer)
        {
            i32 expectedSequence = 0;
            for (const auto& item : trace)
            {
                if (item.first == producer) CHECK(item.second == expectedSequence++);
            }
            CHECK(expectedSequence == TASKS_PER_PRODUCER);
        }
        executor.CompleteTasks();
        CHECK(trace.size() == PRODUCERS * TASKS_PER_PRODUCER);
    }, 10000);
}
