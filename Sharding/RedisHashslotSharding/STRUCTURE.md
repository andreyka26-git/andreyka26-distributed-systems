# Project Structure Reorganization

## New Folder Structure

```
RedisHashslotSharding/
├── Domain/                     # Core business logic and domain models
│   ├── HashService.cs         # Hash slot computation service
│   ├── InMemoryCache.cs       # Cache implementation
│   ├── LocalNode.cs           # Local node implementation
│   ├── NodeBase.cs            # Abstract node base class
│   └── Server.cs              # Main server logic with GetSnapshotAsync
├── Dtos/                      # Data Transfer Objects for API
│   ├── ApiResponses.cs        # Request/Response DTOs
│   ├── HashSlotSnapshot.cs    # Hash slot snapshot DTO
│   └── ServerSnapshot.cs      # Server snapshot DTO
├── Program.cs                 # Application entry point
└── SerialExecutionMiddleware.cs
```

## Changes Made

1. **Renamed DTOs → Dtos**: Changed folder name from "DTOs" to "Dtos" following C# naming conventions
2. **Created Domain folder**: Moved all core business logic from Models and Services folders to Domain
3. **Updated namespaces**: 
   - `RedisHashslotSharding.DTOs` → `RedisHashslotSharding.Dtos`
   - `RedisHashslotSharding.Models` → `RedisHashslotSharding.Domain`
   - `RedisHashslotSharding.Services` → `RedisHashslotSharding.Domain`
4. **Cleaned up**: Removed old Models and Services folders

## Domain Layer (Core Logic)
- **HashService**: Computes hash slots using CRC32
- **InMemoryCache**: Stores key-value pairs in memory
- **NodeBase**: Abstract base for nodes
- **LocalNode**: Local node implementation with cache
- **Server**: Main server with GetSnapshotAsync method that returns non-empty slots and counts

## DTO Layer (API Contracts)
- **HashSlotSnapshot**: Individual slot data
- **ServerSnapshot**: Complete server state snapshot
- **ApiResponses**: Structured API request/response objects

The project now has a clean separation between domain logic and data transfer objects, making it easier to maintain and extend.