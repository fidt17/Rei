cmake_minimum_required(VERSION 3.22)

project(ReiEngine LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

option(REI_BUILD_SANDBOX "Build ReiSandbox executable" ON)

add_subdirectory(Rei)

if(REI_BUILD_SANDBOX)
    add_subdirectory(ReiSandbox)
endif()
