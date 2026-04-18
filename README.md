# Shindig - Game Matchmaking Template

Shindig is a template project that serves as a basic example of a game matchmaking service for multiplayer backends.

It is designed as a lightweight reference for teams exploring event-driven game matchmaking with .NET, Kafka, Redis, and LocalStack-backed local infrastructure.

## Purpose

This repository is intended to show the foundations of a reusable, game-agnostic matchmaking system:

- accepting player queue requests
- publishing queue lifecycle events
- coordinating queue state safely
- consuming queue events and creating basic matches

This repository is specifically about player queues, match formation, and online game infrastructure.

This is best treated as a starter template and learning project rather than a finished production platform.

## Services

The template currently includes or defines the following services:

### API Gateway
- expected entry point for game backends or game servers
- responsible for authentication, validation, and routing
- represented in the architecture design for deployment in front of the producer service

### MatchMakerProducerWebService
- implemented in this repository
- exposes health, join, leave, update, cancel, and queue inspection endpoints
- publishes matchmaking lifecycle events to Kafka
- uses Redis to suppress duplicate concurrent queue operations
- can return the players currently stored in a named queue

### Kafka
- acts as the event transport layer
- stores queue lifecycle events durably
- allows downstream workers and services to consume matchmaking activity

### Redis
- acts as the fast coordination and state layer
- tracks player queue membership
- helps prevent duplicate handling during concurrent requests

### MatchMakerConsumerWorker
- implemented in this repository as a basic worker service
- consumes queue lifecycle events from Kafka
- maintains partition-based candidate pools in Redis
- creates simple matches when enough compatible players are available
- publishes match-created events for downstream consumers

### Downstream Services
Examples shown in the architecture include:
- session or server allocation
- player notifications
- analytics and reporting

## What is implemented today

Current repository capabilities include:

- a .NET producer web service
- a .NET consumer worker service
- queue lifecycle endpoints
- a queue lookup endpoint for viewing currently queued players by queue name
- Kafka event publishing and consumption
- Redis-backed concurrency protection and player pooling
- basic match creation for compatible queued players
- LocalStack-oriented infrastructure templates and scripts
- automated unit and component tests

## Project structure

- src/Matchmaking.ProducerWebService — HTTP producer service
- src/Matchmaking.ConsumerWorker — Kafka consumer and basic match creation worker
- src/Matchmaking.Infrastructure — shared infrastructure code
- tests/Matchmaking.Infrastructure.Tests — unit and component tests
- infra/cloudformation — local infrastructure templates
- scripts — local provisioning helpers for Kafka, Redis, and deployment
- docs — architecture and technical design notes

## Local development

### Prerequisites
- .NET 10 SDK
- Docker Desktop installed and running
- PowerShell

### Getting started

1. Start Docker Desktop and wait for the local container engine to be fully running.
2. Deploy the local infrastructure:

```powershell
pwsh ./scripts/deploy-localstack-producer-web.ps1
```

3. Start the producer service:

```powershell
dotnet run --project ./src/Matchmaking.ProducerWebService
```

4. Open the API test page in your browser:

```text
http://localhost:5000/scalar/v1
```

Use this page to confirm the service is up and to try the available endpoints. You can also quickly verify the health payload at http://localhost:5000/health.

Example routes now available in the API page include:
- GET /health
- POST /matchmaking/join
- POST /matchmaking/leave
- POST /matchmaking/update
- POST /matchmaking/cancel
- GET /matchmaking/queues/{queueName}/players

5. Start the consumer worker in a separate terminal:

```powershell
dotnet run --project ./src/Matchmaking.ConsumerWorker
```

### Run the test suite

```powershell
dotnet test
```

## Using the SDK in a game backend

A game developer can reference the SDK project and use the provided HTTP client from their title backend service.

Typical flow:

1. create an `HttpClient` pointing at the producer service
2. create a `MatchmakingApiClient`
3. queue, update, leave, or cancel players as the game session changes

Example:

```csharp
using Matchmaking.SDK.Matchmaking;

var httpClient = new HttpClient
{
    BaseAddress = new Uri("http://localhost:5000")
};

var matchmakingClient = new MatchmakingApiClient(httpClient);

await matchmakingClient.QueuePlayerAsync(
    playerId: "player-001",
    queueName: "default-queue",
    region: "local-dev",
    gameMode: "casual-duo",
    skillBracket: "bronze",
    attributes: new Dictionary<string, string>
    {
        ["preferredRole"] = "support",
        ["inputType"] = "controller"
    },
    metadata: new Dictionary<string, string>
    {
        ["ticketId"] = "ticket-1001"
    },
    cancellationToken: CancellationToken.None);
```

From there, the title backend can:

- call `UpdatePlayerAsync` when player preferences change
- call `LeaveQueueAsync` when a player exits matchmaking normally
- call `CancelQueueAsync` when the search should be terminated immediately
- call the queue inspection endpoint from the API page to view current queue state during development

## Roadmap

Suggested next steps for the template are:

- add richer rule-driven matchmaking behavior
- support configurable matchmaking rules
- add deployment examples for cloud hosting
- improve operational documentation for open-source contributors

## Open source intent

This repository is published as an open source template to help others bootstrap a simple matchmaking service and adapt it to their own game or backend stack.

## License

MIT
