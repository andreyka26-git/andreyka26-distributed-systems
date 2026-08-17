CXX      ?= g++
CXXFLAGS ?= -std=c++17 -O2 -Wall -Wextra -g

all: blocking_server epoll_server epoll_server_et

blocking_server: blocking_server.cpp
	$(CXX) $(CXXFLAGS) -pthread -o $@ $<

epoll_server: epoll_server.cpp
	$(CXX) $(CXXFLAGS) -o $@ $<

# Same source, edge-triggered instead of level-triggered.
epoll_server_et: epoll_server.cpp
	$(CXX) $(CXXFLAGS) -DUSE_EPOLLET -o $@ $<

clean:
	rm -f blocking_server epoll_server epoll_server_et

.PHONY: all clean
