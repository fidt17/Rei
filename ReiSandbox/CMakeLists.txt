set(REI_ROOT_DIR "${CMAKE_CURRENT_SOURCE_DIR}/..")

add_executable(ReiSandbox
    "${CMAKE_CURRENT_SOURCE_DIR}/ReiApp.cpp"
    "${CMAKE_CURRENT_SOURCE_DIR}/Internal/BehaviourRegistry.cpp"
)

target_include_directories(ReiSandbox
    PRIVATE
        "${REI_ROOT_DIR}/Rei/src"
        "${REI_ROOT_DIR}/Rei/external"
        "${REI_ROOT_DIR}/Rei/resources"
)

target_compile_definitions(ReiSandbox
    PRIVATE
        REI_APP
        $<$<CONFIG:Debug>:DEBUG>
        $<$<CONFIG:Release>:RELEASE>
)

if(MSVC)
    target_compile_options(ReiSandbox PRIVATE /W3 /permissive- /FS /FIRei.h)
endif()

target_link_libraries(ReiSandbox PRIVATE Rei)

set(REI_ASSIMP_DLL "${REI_ROOT_DIR}/Rei/external/assimp/assimp-vc143-mt.dll")

add_custom_command(TARGET ReiSandbox POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E copy_if_different
        "$<TARGET_FILE:Rei>"
        "$<TARGET_FILE_DIR:ReiSandbox>"
    COMMAND ${CMAKE_COMMAND} -E copy_if_different
        "${REI_ASSIMP_DLL}"
        "$<TARGET_FILE_DIR:ReiSandbox>"
)
