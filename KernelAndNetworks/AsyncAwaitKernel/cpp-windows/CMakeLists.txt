cmake_minimum_required(VERSION 3.10)
project(async_await_kernel_windows CXX)

if(NOT WIN32)
  message(FATAL_ERROR "This directory is Windows-only (Winsock, WSAPoll). "
                      "Use ../cpp-linux or ../cpp-crossplatform.")
endif()

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# No EDGE_TRIGGERED option here: WSAPoll has no edge mode. epoll's EPOLLET and kqueue's
# EV_CLEAR have no Windows equivalent - see the header of epoll_server.cpp.

find_package(Threads REQUIRED)

add_executable(blocking_server blocking_server.cpp)
add_executable(epoll_server epoll_server.cpp)

foreach(target blocking_server epoll_server)
  target_link_libraries(${target} PRIVATE Threads::Threads ws2_32)
  if(MSVC)
    target_compile_options(${target} PRIVATE /W4)
  endif()
endforeach()
