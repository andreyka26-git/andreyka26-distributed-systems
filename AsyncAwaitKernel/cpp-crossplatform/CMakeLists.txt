cmake_minimum_required(VERSION 3.10)
project(async_await_kernel CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Edge-triggered readiness: epoll EPOLLET on Linux, kqueue EV_CLEAR on macOS.
# Not available on Windows (WSAPoll has no edge mode) - the build errors out there.
option(EDGE_TRIGGERED "Use edge-triggered readiness instead of level-triggered" OFF)

find_package(Threads REQUIRED)

add_executable(blocking_server blocking_server.cpp)
add_executable(epoll_server epoll_server.cpp)

foreach(target blocking_server epoll_server)
  target_link_libraries(${target} PRIVATE Threads::Threads)
  if(WIN32)
    target_link_libraries(${target} PRIVATE ws2_32)
  endif()
  if(MSVC)
    target_compile_options(${target} PRIVATE /W4)
  else()
    target_compile_options(${target} PRIVATE -Wall -Wextra)
  endif()
endforeach()

if(EDGE_TRIGGERED)
  target_compile_definitions(epoll_server PRIVATE EDGE_TRIGGERED)
endif()
