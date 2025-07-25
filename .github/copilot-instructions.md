# Chuncker - Distributed File Storage System

Act your role as a  senior software architect and developer. Focus on the design and implementation of a distributed file storage system that meets the functional requirements outlined below. Ensure that your code is clean, maintainable, and follows best practices in software development.

## Project Overview
Chuncker is a distributed file storage system built as a .NET Console Application. It splits large files into smaller chunks, distributes these chunks across different storage providers, and ensures file integrity through checksum validation. The system uses a microservices-like architecture with events for communication between components.

## Key Architectural Components
- **Chunk Management**: Files are dynamically split into optimally sized chunks, compressed with Gzip, and processed using memory-mapped files
- **Storage Provider System**: Abstract `IStorageProvider` interface with multiple implementations (FileSystem, MongoDB)
- **Metadata Database**: MongoDB stores all chunk metadata and relationships
- **Caching Layer**: Redis for frequently accessed metadata
- **Logging System**: Event-based logging with correlationId tracking and TTL-based expiration
- **Event System**: Custom IEvent with Publisher/Handler pattern for async communication

## Developer Workflows

### Building and Running
```bash
# Build the project
dotnet build "./Chuncker.sln"

# Run the application
dotnet run --project "./Chuncker/Chuncker.csproj"

# Run with Docker
docker build -t chuncker .
docker run chuncker
```

### Testing
```bash
dotnet test
```

## Core Design Patterns
- **Repository Pattern**: For data access abstraction
- **Dependency Injection**: Using Microsoft.Extensions.DependencyInjection
- **Strategy Pattern**: For different chunk processing strategies
- **Factory Pattern**: For creating appropriate storage providers
- **Event Pattern**: For decoupled component communication
- **Service Layer**: Orchestrating the business logic

## Integration Points
- **MongoDB**: For metadata and logging storage
- **Redis**: For caching layer
- **File System**: One of the storage provider implementations
- **Grafana + Loki**: Log visualization integration (prepared format)

## Project-Specific Conventions
- Always maintain correlationId across all operations and logs
- Use async/await for all I/O operations
- Storage providers must be interchangeable through DI
- All business logic should be in dedicated service classes
- Event handlers must not contain business logic directly

## File Organization
- Core domain logic and interfaces in `/Core` or root namespace
- Storage provider implementations in `/Storage` or `/Providers`
- Event definitions in `/Events`
- Service implementations in `/Services`

## Best Practices
- Always validate file integrity with SHA256 checksums
- Use memory-mapped files for large file operations
- Ensure all providers implement proper disposal patterns
- Log all operations with appropriate correlationId
- Design for testability with proper abstractions

---
_Note: This project is currently in early development stages. The architecture and implementation details are subject to change._
