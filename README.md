# Shindig Game Matchmaking Template

Shindig is a template project that serves as a basic example of a game matchmaking service for multiplayer backends.

It is designed as a lightweight reference for teams exploring event-driven game matchmaking with .NET, Kafka, Redis, and LocalStack-backed local infrastructure.

## Purpose

This repository is intended to show the foundations of a reusable, game-agnostic matchmaking system:

- accepting player queue requests
- publishing queue lifecycle events
- coordinating queue state safely
- consuming queue events and creating basic matches

This repository is specifically about player queues, match formation, and online game infrastructure. It is not intended for dating or social matchmaking scenarios.

This is best treated as a starter template and learning project rather than a finished production platform.

## Services

The template currently includes or defines the following services:

### API Gateway
- expected entry point for game backends or game servers
- responsible for authentication, validation, and routing
- represented in the architecture design for deployment in front of the producer service

### MatchMakerProducerWebService
- implemented in this repository
- exposes health, join, leave, update, and cancel endpoints
- publishes matchmaking lifecycle events to Kafka
- uses Redis to suppress duplicate concurrent queue operations

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
- Docker
- PowerShell

### Run the local infrastructure

```powershell
pwsh ./scripts/deploy-localstack-producer-web.ps1
```

### Run the test suite

```powershell
dotnet test
```

### Start the producer service

```powershell
dotnet run --project ./src/Matchmaking.ProducerWebService
```

### Start the consumer worker

```powershell
dotnet run --project ./src/Matchmaking.ConsumerWorker
```

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
