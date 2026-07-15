cmake_minimum_required(VERSION 3.20)
project(OneBotCodexCompanionEui LANGUAGES C CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
if(MSVC)
    set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
endif()

set(EUI_NEO_SOURCE_DIR "" CACHE PATH "Path to a local EUI-NEO checkout")

if(EUI_NEO_SOURCE_DIR AND EXISTS "${EUI_NEO_SOURCE_DIR}/CMakeLists.txt")
    set(EUI_NEO_ROOT "${EUI_NEO_SOURCE_DIR}")
    add_subdirectory("${EUI_NEO_ROOT}" "${CMAKE_BINARY_DIR}/eui-neo")
else()
    include(FetchContent)
    FetchContent_Declare(
        eui_neo
        GIT_REPOSITORY https://github.com/sudoevolve/EUI-NEO.git
        GIT_TAG 9d090abcd4c0481e6cc6970fdf919e22f56c0ee2
    )
    FetchContent_MakeAvailable(eui_neo)
    set(EUI_NEO_ROOT "${eui_neo_SOURCE_DIR}")
endif()

add_executable(onebot_codex_eui
    "${EUI_NEO_ROOT}/core/app/glfw_app_main.cpp"
    app.cpp
    autostart.cpp
    onebot_client.cpp
    settings_store.cpp
    session_monitor.cpp
)

eui_neo_configure_app(onebot_codex_eui)
target_link_libraries(onebot_codex_eui PRIVATE winhttp crypt32 advapi32)

add_custom_command(TARGET onebot_codex_eui POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E make_directory "$<TARGET_FILE_DIR:onebot_codex_eui>/assets"
    COMMAND ${CMAKE_COMMAND} -E copy_if_different
        "${CMAKE_CURRENT_SOURCE_DIR}/assets/icon.ico"
        "$<TARGET_FILE_DIR:onebot_codex_eui>/assets/icon.ico"
)
