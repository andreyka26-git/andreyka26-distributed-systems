cmake_minimum_required(VERSION 3.10)
project(async_await_kernel_linux CXX)

if(NOT CMAKE_SYSTEM_NAME STREQUAL "Linux")
  message(FATAL_ERROR "This directory is Linux-only (epoll, /proc, gettid). "
                      "Use ../cpp-windows or ../cpp-crossplatform.")
endif()

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Edge-triggered readiness: EPOLLET. Not re-added to rdllist while still ready, so the
# read loop MUST drain to EAGAIN or the event is lost.
option(EDGE_TRIGGERED "Use edge-triggered readiness instead of level-triggered" OFF)

find_package(Threads REQUIRED)

add_executable(blocking_server blocking_server.cpp)
add_executable(epoll_server epoll_server.cpp)

foreach(target blocking_server epoll_server)
  target_link_libraries(${target} PRIVATE Threads::Threads)
  target_compile_options(${target} PRIVATE -Wall -Wextra)
endforeach()

if(EDGE_TRIGGERED)
  target_compile_definitions(epoll_server PRIVATE EDGE_TRIGGERED)
endif()
