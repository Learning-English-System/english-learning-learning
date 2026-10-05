# English Learning - Learning Service

Independent microservice repository managing vocabulary, Spaced Repetition System (SuperMemo SM-2 algorithm), learning progress, and internal gRPC endpoints.

## Architecture & Responsibilities
- **Domain**: SM-2 review scheduler algorithm, vocabulary aggregates, review states, categories, examples.
- **Application**: Vocabulary retrieval, search, progress submission, review scheduling logic.
- **Infrastructure**: Entity Framework Core with SQL Server, Redis distributed caching, Kafka event publishing (Outbox pattern), and gRPC server implementation for internal inter-service queries.
- **Contracts**: Exports `EnglishLearning.Learning.Contracts` NuGet package containing integration events and DTOs.
- **Api**: REST APIs on port 5003 and gRPC server for low-latency queries.

## Getting Started

### Prerequisites
- .NET 10 SDK
- SQL Server
- Redis Cache
- Kafka broker

### Build, Test & Run
```bash
# Restore & build
dotnet restore LearningService.slnx
dotnet build LearningService.slnx

# Run tests
dotnet test LearningService.slnx

# Pack shared contracts
dotnet pack src/Learning.Contracts/Learning.Contracts.csproj -c Release -o ../../packages

# Run API
dotnet run --project src/Learning.Api
```

### Docker
```bash
docker build -t english-learning-learning:latest .
docker run -p 5003:5003 english-learning-learning:latest
```
