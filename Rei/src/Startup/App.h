#pragma once

namespace rei
{
    class REI_API App
    {
    public:
        void Start();

        int GetAppNumber() const;

    private:
        int _appNumber = 0;
    };
}
