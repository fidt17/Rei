#pragma once

class MyBehaviour : public rei::Behaviour
{
private:
    BEHAVIOUR_BODY(MyBehaviour)

    SERIALIZED bool _flag;
    SERIALIZED int _counter;
    SERIALIZED std::string _msg;
    
public:
    void Init() override
    {
        LOG(STRING(_flag) + " " + STRING(_counter) + " " + _msg)
    }
};
