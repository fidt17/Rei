#pragma once
#include <rei_behaviours/render/light/PointLight.h>

class ColorLerp : public rei::Behaviour
{
private:
    BEHAVIOUR_BODY(ColorLerp)

    SERIALIZE i32 _targetLightId;
    SERIALIZE rei::render::Color _from;
    SERIALIZE rei::render::Color _to;
    SERIALIZE f32 _speed;

    f32 _progress = 0;
    f32 _direction = 1;
    
public:

    void Update() override
    {
        const auto& e = rei::GetEntityManager().GetBySceneId(_targetLightId);
        if (e == rei::ecs::NULL_ENTITY) return;

        ECS_WORLD(rei::GetInternalWorld());
        GET(e, rei::render::PointLight).SetColor(rei::render::Color::Lerp(_from, _to, _progress));

        _progress += _speed * _direction;
        if (_progress > 1)
        {
            _progress = 1;
            _direction = -1;
        }
        else if (_progress < 0)
        {
            _progress = 0;
            _direction = 1;
        }
    }
    
};
