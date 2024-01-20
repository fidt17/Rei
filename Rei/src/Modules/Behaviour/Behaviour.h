#pragma once

#define BEHAVIOUR(NAME)\
    class NAME : public rei::Behaviour  // NOLINT(bugprone-macro-parentheses)

namespace rei
{
    class Behaviour
    {
    public:
        virtual ~Behaviour() = default;
        virtual void Init() = 0;
    };
}
