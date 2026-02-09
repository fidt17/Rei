file(GLOB_RECURSE REI_ENGINE_SOURCES CONFIGURE_DEPENDS
    "${CMAKE_CURRENT_SOURCE_DIR}/src/*.cpp"
    "${CMAKE_CURRENT_SOURCE_DIR}/resources/rei_behaviours/*.cpp"
)

list(REMOVE_ITEM REI_ENGINE_SOURCES
    "${CMAKE_CURRENT_SOURCE_DIR}/src/Modules/Render/Textures/Texture2D.cpp"
)

set(REI_THIRD_PARTY_SOURCES
    "${CMAKE_CURRENT_SOURCE_DIR}/external/glad.cpp"
    "${CMAKE_CURRENT_SOURCE_DIR}/external/glm/detail/glm.cpp"
)

add_library(Rei SHARED
    ${REI_ENGINE_SOURCES}
    ${REI_THIRD_PARTY_SOURCES}
)

set(REI_EXTERNAL_DIR "${CMAKE_CURRENT_SOURCE_DIR}/external")
set(REI_GLFW_LIB "${REI_EXTERNAL_DIR}/glfw/lib-vc2022/glfw3.lib")
set(REI_ASSIMP_LIB "${REI_EXTERNAL_DIR}/assimp/assimp-vc143-mt.lib")
set(REI_ASSIMP_DLL "${REI_EXTERNAL_DIR}/assimp/assimp-vc143-mt.dll")

target_include_directories(Rei
    PRIVATE
        "${CMAKE_CURRENT_SOURCE_DIR}/src"
        "${REI_EXTERNAL_DIR}"
        "${CMAKE_CURRENT_SOURCE_DIR}/resources"
)

target_compile_definitions(Rei
    PRIVATE
        REI_ENGINE
        $<$<CONFIG:Debug>:DEBUG>
        $<$<CONFIG:Release>:RELEASE>
)

if(MSVC)
    target_compile_options(Rei PRIVATE /W3 /permissive- /FS /FIpch.h /FICore.h)
endif()

target_link_libraries(Rei
    PRIVATE
        opengl32
        "${REI_GLFW_LIB}"
        "${REI_ASSIMP_LIB}"
)

add_custom_command(TARGET Rei POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E copy_if_different
        "${REI_ASSIMP_DLL}"
        "$<TARGET_FILE_DIR:Rei>"
)
