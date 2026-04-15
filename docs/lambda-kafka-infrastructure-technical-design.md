# Lambda to Kafka Ping Infrastructure – Technical Design

## 1. Overview

### Goal
Stand up the first slice of infrastructure for the matchmaking platform using a very small Lambda function that publishes a lightweight ping event into Kafka.

This validates the core platform wiring before building the full matchmaking service:

- Lambda packaging and deployment
- runtime configuration and secrets
- network access to Kafka
- topic creation and write permissions
- local development workflow using LocalStack
- basic observability and failure handling

### Problem Statement
Before implementing queue management and match formation, the team needs a minimal, low-risk infrastructure proof point. The simplest useful step is a Lambda that sends a test event into Kafka on demand or on a schedule.

### Non-Goals
This phase does not include:

- player matchmaking logic
- rule engine behavior
- Redis-backed queue state
- server allocation
- player notification workflows
- full downstream consumers

---

## 2. Design Summary

The initial infrastructure will consist of:

1. a small Lambda function
2. a Kafka topic used as the first event queue
3. LocalStack for local AWS service emulation
4. optional EventBridge scheduling or manual invocation
5. logging and metrics for operational validation

For local development, LocalStack will emulate the AWS pieces such as Lambda, SQS, and parameter/config support, while Kafka will run as a local broker or managed cluster endpoint depending on the environment.

> For the MVP, the "queue" can be the Kafka topic itself. If an AWS queue is later required, an SQS handoff can be added without changing the Lambda event contract.

---

## 3. Objectives

### Primary Objectives
- Prove that the platform can publish events from serverless infrastructure into Kafka
- Establish a repeatable local development workflow
- Create the base deployment pattern for future matchmaking components
- Verify connectivity, authentication, and topic conventions early

### Success Criteria
The design is considered successful when:

- the Lambda can be invoked locally and in the target environment
- a ping message is successfully published to the Kafka topic
- the publish outcome is visible through logs and metrics
- failures are retried or surfaced clearly
- the same configuration model works in LocalStack and higher environments

---

## 4. High-Level Architecture

### Core Components

#### 4.1 Ping Lambda
A lightweight Lambda function responsible for:

- accepting a scheduled or manual trigger
- creating a small ping payload
- publishing that payload to a Kafka topic
- logging the result with correlation identifiers

#### 4.2 Kafka Topic
A dedicated topic will act as the initial event queue.

Suggested topic name:
- `mm.infrastructure.ping`

This topic is used to:
- validate producer connectivity
- verify topic configuration and permissions
- establish naming conventions for the broader platform

#### 4.3 LocalStack
LocalStack will be used to emulate the AWS-facing parts of the platform during local development.

Recommended uses:
- Lambda execution
- SQS for optional validation queue testing
- parameter and secret bootstrap if desired
- reproducible local infrastructure startup

#### 4.4 Optional Validation Queue
If the team wants a visible queue-based confirmation path, a second step can be added later:

- a simple consumer reads from `mm.infrastructure.ping`
- it writes a confirmation event to an SQS queue such as `mm-infra-ping-results`

This is optional for the first milestone.

---

## 5. Request and Event Flow

### Option A: Simplest MVP Flow
1. Lambda is invoked manually or by a schedule.
2. Lambda builds a ping event.
3. Lambda publishes the event to Kafka topic `mm.infrastructure.ping`.
4. Logs confirm success or failure.

### Option B: Extended Validation Flow
1. Lambda publishes the ping event to Kafka.
2. A small validation consumer reads the event.
3. The consumer writes a confirmation message to an SQS queue.
4. Operators or tests assert end-to-end delivery.

Option A is sufficient for the first implementation.

---

## 6. Event Contract

### Ping Event Example

```json
{
  "eventType": "INFRA_PING",
  "timestamp": "2026-04-15T05:00:00Z",
  "source": "lambda-kafka-ping",
  "environment": "local",
  "correlationId": "7f0b1e84-4d5a-4d63-9a8c-14f27f9b0d10",
  "metadata": {
    "service": "generic-matchmaking-infra",
    "purpose": "connectivity-check"
  }
}
```

### Required Fields
- `eventType`
- `timestamp`
- `source`
- `environment`
- `correlationId`

These fields allow traceability across logs and future consumers.

---

## 7. Infrastructure Components

### 7.1 Lambda Function
Suggested responsibilities:
- load configuration from environment variables
- initialize Kafka producer
- construct ping event
- publish to topic
- emit structured logs
- return a simple success or failure response

Suggested configuration:
- `KAFKA_BOOTSTRAP_SERVERS`
- `KAFKA_TOPIC`
- `KAFKA_CLIENT_ID`
- `ENVIRONMENT`
- `LOG_LEVEL`

### 7.2 Kafka Broker / Cluster
For the first milestone, Kafka should expose:
- broker endpoint(s)
- topic creation support
- producer authentication if needed
- retention suitable for diagnostic events

Suggested topic defaults:
- low partition count for MVP
- replication based on environment capabilities
- short retention for ping traffic

### 7.3 LocalStack Resources
For local development, provision:
- Lambda runtime container
- optional SQS queue for validation
- local configuration bootstrap scripts

### 7.4 IAM and Access
The Lambda should use the minimum permissions needed to:
- read configuration and secrets
- write logs
- optionally interact with an SQS validation queue

Kafka authorization should be restricted to producer access on the required topic only.

---

## 8. Local Development Model

### Why LocalStack
LocalStack lets the team validate the AWS serverless workflow locally without needing shared cloud resources for every iteration.

### Recommended Local Setup
- LocalStack for Lambda and optional SQS
- Docker-based Kafka broker such as Kafka or Redpanda
- environment file for broker endpoints and topic names
- local invoke script for repeatable testing

### Local Flow
1. Start LocalStack and Kafka locally.
2. Provision or verify the Kafka topic.
3. Deploy the Lambda to LocalStack.
4. Invoke the Lambda with a test payload.
5. Verify the message appears on the Kafka topic.
6. If the optional SQS flow is enabled, verify the confirmation queue receives a result message.

---

## 9. Reliability and Failure Handling

### Failure Scenarios
- Kafka broker unreachable
- invalid credentials or TLS config
- topic missing
- serialization failure
- Lambda timeout

### Handling Strategy
- use short retries inside the producer client
- fail fast if configuration is invalid
- emit structured error logs with correlation ID
- configure a dead-letter queue later if retries are exhausted
- keep the Lambda idempotent so repeated invocations are safe

For this phase, observability is more important than aggressive retry behavior.

---

## 10. Observability

### Logging
The Lambda should emit structured logs for:
- invocation start
- resolved environment
- topic name
- publish attempt
- publish success with correlation ID
- publish failure with error category

### Metrics
Track at minimum:
- invocation count
- success count
- failure count
- publish latency
- timeout count

### Health Validation
A simple smoke test should verify:
- the Lambda can start
- Kafka is reachable
- one ping event can be written successfully

---

## 11. Security Considerations

- keep broker endpoints and credentials in environment-backed configuration or secrets storage
- avoid logging secrets or full connection strings
- use TLS and SASL in higher environments if required by the Kafka platform
- restrict Lambda network access to only the necessary broker endpoints
- use least-privilege IAM for any AWS resource interactions

---

## 12. Deployment Approach

### MVP Deployment Steps
1. Provision Kafka topic `mm.infrastructure.ping`.
2. Configure Lambda environment variables.
3. Deploy the Lambda package.
4. Invoke it manually to validate connectivity.
5. Add a periodic schedule once manual verification is complete.

### Rollout Strategy
- start in local development with LocalStack
- promote to a shared non-production environment
- validate logs and metrics
- use the pattern as the base for later matchmaking producers and consumers

---

## 13. MVP Scope

The first version should include only:

- one Lambda function
- one Kafka topic for ping traffic
- LocalStack-based local execution
- structured logging
- manual invocation or very simple schedule

This is intentionally narrow so the infrastructure path can be proven quickly.

---

## 14. Future Enhancements

After the MVP is working, the next improvements can be:

- SQS confirmation queue for end-to-end validation
- dead-letter queue support
- infrastructure as code templates
- CI integration test that invokes the Lambda against LocalStack
- promotion of the same event pattern into the matchmaking join and leave flows

---

## 15. Summary

This design establishes the first infrastructure milestone for the generic matchmaking platform: a small Lambda that publishes a ping event into Kafka. It is deliberately minimal, easy to validate, and compatible with LocalStack-based local development. Once this path is working reliably, the same deployment and messaging pattern can be extended into the full matchmaking service.
