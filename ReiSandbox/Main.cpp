#include <assert.h>
#include <Ecs/Ecs.h>

#include "Ecs/World.h"

struct Position
{
    int X;
    int Y;
};

struct Velocity
{
    int XSpeed;
    int YSpeed;
};

struct IgnoreVelocityTag
{
};

// Gets called when application starts
void OnProjectStart()
{
    using namespace rei::ecs;

    World world;
    auto f = world.CreateFilter();
    world.Include<Position>(f);
    world.Include<Velocity>(f);
    world.Exclude<IgnoreVelocityTag>(f);

    Entity e1 = world.CreateEntity();
    world.GetComponent<Position>(e1) = {0, 0};
    world.GetComponent<Velocity>(e1) = {1, -1};

    for (int i = 0; i < 100; i++)
    {
        const auto& position = world.GetComponent<Position>(e1);
        LOG(std::to_string(position.X) + " " + std::to_string(position.Y))

        if (i % 2 == 0)
        {
            world.AddComponent<IgnoreVelocityTag>(e1);
        }
        else
        {
            world.DeleteComponent<IgnoreVelocityTag>(e1);
        }
        
        for (auto e : f->EntitiesList)
        {
            auto& pos = world.GetComponent<Position>(e);
            const auto& velocity = world.GetComponent<Velocity>(e);

            pos.X += velocity.XSpeed;
            pos.Y += velocity.YSpeed;
        }
        world.UpdateWorld();
    }
}

// Gets called when application shutdowns
void OnProjectShutdown()
{
}
