# Generic Matchmaking Service – Technical Design

## 1. Overview

### Goal
Build a reusable, game-agnostic matchmaking service that:

- Consumes player queue events from Kafka
- Maintains queues and player entries
- Applies configurable rules to form matches
- Publishes match events to Kafka

### Out of Scope
This service does **not**:

- Allocate game servers
- Compute or own MMR logic
- Contain game-specific matchmaking behavior
- Notify players directly

Its responsibility is limited to managing queues and emitting match results for downstream systems.

---

## 2. High-Level Architecture

### Core Components

#### 2.1 API Gateway
- Accepts HTTP requests from game backends or game servers
- Authenticates and validates incoming payloads
- Routes matchmaking requests to the producer web service

#### 2.2 MatchMakerProducerWebService (EC2)
- Receives join, leave, update, and cancel requests from the API Gateway
- Enriches requests with timestamps and routing metadata
- Publishes queue lifecycle events into Kafka running on EC2

#### 2.3 Kafka Cluster (EC2)
- Serves as the event transport layer between the producer and worker services
- Stores queue-related events durably for replay and recovery
- Partitions queue events by the composite key `region + gameMode + skillBracket` so similar candidates are routed together

#### 2.4 MatchMakerConsumerWorker (EC2)
- Consumes queue events from Kafka
- Uses Redis as the authoritative source of queue and pool state
- Maintains the active player pool for each partition key
- In the current template implementation, forms a basic match when the configured number of compatible players are available in the same partition
- Provides the foundation for future wait-time expansion and richer rule evaluation
- Emits `MatchCreated` events back into Kafka for downstream consumers

#### 2.5 Redis
- Acts as the fast, in-memory authoritative queue and matchmaking state store
- Coordinates multiple consumer workers to avoid duplicate handling
- Supports queue membership, pool updates, lock/ownership patterns, and match lifecycle state

#### 2.6 Downstream Consumers
These are outside the matchmaking service boundary:

- **Session / Server Allocator**: consumes match-created events and provisions runtime resources
- **Notification Service**: informs players of queue and match state
- **Analytics Pipeline**: records queue and match metrics

### Architecture Summary

1. Client-facing systems call the API Gateway.
2. The API Gateway forwards requests to the MatchMakerProducerWebService.
3. The producer service publishes queue events to Kafka.
4. MatchMakerConsumerWorker instances consume those events and update Redis.
5. Redis remains the authoritative source of queue and pool state while Kafka feeds the event stream.
6. Workers consume queue events, maintain Redis-backed pools, and form basic matches for compatible players.
7. When a valid match is found, the worker emits a `MatchCreated` event back into Kafka.
8. Downstream systems react to matchmaking and queue lifecycle events.

---

## 3. Kafka Topics

### 3.1 Input Topics

#### mm.player.queue
Primary topic for player queue lifecycle events.

Supported event types:
- `PLAYER_JOIN_QUEUE`
- `PLAYER_LEAVE_QUEUE`
- `PLAYER_UPDATE_ATTRIBUTES`
- `PLAYER_DEQUEUED`
- `MATCH_CANCELLED`

#### mm.queue.config (optional)
Used only if queue configuration becomes dynamic at runtime.

Possible uses:
- Add or remove queues
- Change rules without redeploying
- Tune parameters such as player limits or wait expansion

### 3.2 Output Topics

#### mm.match.created
Published whenever a valid match is formed.

#### mm.player.dequeued
Published when a player leaves the queue, disconnects, or cancels matchmaking.

#### mm.match.cancelled
Published when a previously forming or formed match must be cancelled.

#### mm.match.failed (optional)
Can be used for timeout, expiry, or terminal failure notifications in later phases.

---

## 4. Data Models

### 4.1 Player Queue Event

```json
{
  "eventType": "PLAYER_JOIN_QUEUE",
  "timestamp": "2026-04-15T04:40:00Z",
  "playerId": "player-123",
  "queueName": "default",
  "attributes": {
    "mmr": 1420,
    "region": "oce",
    "latency": 32,
    "partySize": 1,
    "skillBracket": "1400-1499"
  },
  "metadata": {
    "gameId": "game-xyz",
    "modeId": "mode-duo"
  },
  "partitionKey": "oce:mode-duo:1400-1499"
}
```

### 4.2 Queue Configuration

```json
{
  "queueName": "default",
  "minPlayers": 2,
  "maxPlayers": 10,
  "maxWaitSeconds": 60,
  "rules": [
    "mmr_range",
    "latency_region",
    "party_size_constraint",
    "wait_time_expansion"
  ],
  "ruleConfig": {
    "mmr_range": { "initialRange": 100, "expandPerSecond": 5 },
    "latency_region": { "allowedRegions": ["oce", "sea"] },
    "party_size_constraint": { "maxPartySize": 4 }
  }
}
```

### 4.3 Match Created Event

```json
{
  "eventType": "MATCH_CREATED",
  "timestamp": "2026-04-15T04:40:05Z",
  "matchId": "match-abc-001",
  "queueName": "default",
  "players": [
    {
      "playerId": "player-123",
      "attributes": {
        "mmr": 1420,
        "region": "oce",
        "latency": 32,
        "partySize": 1
      }
    }
  ],
  "metadata": {
    "gameId": "game-xyz",
    "modeId": "mode-duo"
  }
}
```

### 4.4 Internal Types

```ts
type PlayerEntry = {
  playerId: string;
  queueName: string;
  attributes: Record<string, unknown>;
  metadata?: Record<string, unknown>;
  joinedAt: Date;
  updatedAt: Date;
};

type QueueConfig = {
  queueName: string;
  minPlayers: number;
  maxPlayers: number;
  maxWaitSeconds?: number;
  rules: string[];
  ruleConfig?: Record<string, unknown>;
};
```

---

## 5. Internal Components

### 5.1 Redis Queue and Pool Manager

#### Responsibilities
- Maintain authoritative player state per queue and partition key in Redis
- Handle add, remove, update, dequeue, and cancellation operations
- Keep queue ordering stable by join time while enabling fast worker coordination
- Support efficient lookup by player ID and pool membership

#### Suggested Structures
- Redis sorted sets keyed by `region:gameMode:skillBracket` for waiting players
- Redis hashes for direct player state access
- Redis locks or leases for consumer coordination
- Redis records for in-flight match lifecycle state

#### Event Handling
- **PLAYER_JOIN_QUEUE** → add player to the Redis-backed queue
- **PLAYER_LEAVE_QUEUE** → remove player from the queue
- **PLAYER_UPDATE_ATTRIBUTES** → mutate player attributes in place
- **PLAYER_DEQUEUED** → remove a player because they left, disconnected, or cancelled
- **MATCH_CANCELLED** → return impacted players to the appropriate state or clear match state

### 5.2 Rule Engine

#### Design Principle
Rules should be implemented as pure functions so they are:
- Easy to test
- Composable
- Queue-config driven
- Independent of game-specific logic

#### Interface
```ts
type Rule = (
  candidates: PlayerEntry[],
  queueConfig: QueueConfig,
  now: Date
) => PlayerEntry[] | null;
```

#### Example Rules
- `mmr_range`
- `latency_region`
- `party_size_constraint`
- `wait_time_expansion`

#### Rule Processing Model
1. Select a candidate group
2. Apply rules in configured order
3. Return a valid group if all rules pass
4. Return `null` if any rule rejects the group

### 5.3 Matchmaker Loop

#### Responsibilities
- Periodically evaluate active Redis-backed pools
- Select candidate groups within configured size limits for each `region + gameMode + skillBracket` partition
- Pass groups through the rule engine
- Widen acceptable skill tolerance over time for long-waiting players
- Emit events for successful matches
- Remove matched players from the authoritative queue state

#### Pseudocode
```ts
for (const pool of redisPools) {
  const players = pool.waitingPlayers;

  while (players.length >= pool.minPlayers) {
    const expandedTolerance = calculateTolerance(players, now);
    const candidateGroup = selectCandidates(players, expandedTolerance, pool.maxPlayers);
    const validGroup = ruleEngine.apply(candidateGroup, pool.config, now);

    if (validGroup) {
      persistMatchState(validGroup, pool);
      emitMatchCreated(validGroup, pool);
      removeFromQueue(validGroup, pool);
    } else {
      break;
    }
  }
}
```

#### Loop Tuning
Typical loop interval can start at 100–500 ms and be tuned based on:
- queue volume
- acceptable latency
- CPU utilization
- number of active partitions

---

## 6. Minimal API Surface

Although Kafka is the system backbone, a small API Gateway plus producer web service layer improves adoption and integration.

### Endpoints
- `POST /matchmaking/join`
- `POST /matchmaking/leave`
- `POST /matchmaking/update`
- `POST /matchmaking/cancel`

### Endpoint Responsibilities
Each endpoint should:
1. Validate request shape and required fields
2. Enrich with timestamp and metadata if needed
3. Publish the corresponding event to `mm.player.queue`
4. Return an acknowledgement response

### Example Join Request

```json
{
  "playerId": "player-123",
  "queueName": "default",
  "attributes": {
    "mmr": 1420,
    "region": "oce",
    "latency": 32,
    "partySize": 1
  },
  "metadata": {
    "gameId": "game-xyz",
    "modeId": "mode-duo"
  }
}
```

---

## 7. Processing Flow

### Join Flow
1. A game backend submits a join request.
2. The API Gateway forwards the request to the MatchMakerProducerWebService.
3. The producer publishes a `PLAYER_JOIN_QUEUE` event to Kafka using the composite partition key.
4. A MatchMakerConsumerWorker consumes the event and writes the authoritative state to Redis.
5. The worker evaluates the Redis-backed pool during the next scan.
6. If a valid group is found, the worker emits `MATCH_CREATED` back into Kafka.

### Leave / Dequeue Flow
1. A leave, disconnect, or matchmaking cancel request is submitted.
2. A `PLAYER_LEAVE_QUEUE` or `PLAYER_DEQUEUED` event is published.
3. A consumer worker removes the player from Redis if present.
4. Downstream consumers can react to the dequeue event if needed.

### Update Flow
1. A player attribute update is submitted.
2. A `PLAYER_UPDATE_ATTRIBUTES` event is published.
3. The consumer worker updates the Redis-backed player entry.
4. The new values are considered in future matching passes.

### Match Cancellation Flow
1. A match can no longer proceed because players disconnected, left, or a cancellation was triggered.
2. A `MATCH_CANCELLED` event is published.
3. The consumer worker updates Redis match state and re-queues players when appropriate.
4. Downstream systems react to the cancellation event.

---

## 8. Non-Functional Considerations

### Scalability
- Partition `mm.player.queue` by the composite key `region + gameMode + skillBracket`
- Run multiple consumer worker instances in the same consumer group
- Keep pool ownership deterministic to avoid duplicate matching

### State Storage
- Use Redis as the authoritative, fast in-memory queue and state store
- Let consumer workers coordinate through Redis for ownership and consistency
- Treat Kafka as the durable event feed and replay source rather than the queue-of-record

### Reliability
- Use idempotent handling for duplicate queue events
- Prevent the same player from existing in the same Redis-backed queue more than once
- Ensure match emission and queue removal are coordinated to avoid duplicate matches
- Use Redis coordination primitives so multiple workers do not claim the same pool segment
- Validate the end-to-end join-to-match event flow locally before promoting changes to shared environments

### Local Development and Testing
- Run Kafka and Redis locally through a reproducible container-based setup
- Provision local topics and Redis structures through startup scripts so the environment is reproducible
- Run smoke tests locally to verify join, leave, dequeue, cancel, and match-created flows
- Keep application configuration environment-driven so switching between local and EC2-hosted environments is low risk

### Observability
Track at minimum:
- queue length per queue
- average wait time
- match formation rate
- rule rejection counts
- timeout / expiry counts

Logs should include:
- join, leave, and update processing outcomes
- rule evaluation failures
- match creation summaries
- unexpected config or state errors

### Extensibility
- Add new rules without modifying the core loop
- Add new queues through config rather than code changes
- Keep metadata opaque so games can attach their own identifiers

---

## 9. Deployment and Partitioning Strategy

### Recommended Initial Model
- API Gateway in front of the producer web service
- One MatchMakerProducerWebService deployment on EC2
- Kafka cluster on EC2
- One or more MatchMakerConsumerWorker deployments on EC2
- Redis as the shared authoritative queue/state store

### Future Horizontal Scaling
To scale safely:
- Route all events for a given `region + gameMode + skillBracket` combination to the same partition or shard owner
- Ensure one active consumer instance owns a pool segment at a time through Redis coordination
- Avoid split-brain queue state across instances

This lets the service stay simple while preserving deterministic match decisions.

---

## 10. MVP Scope

To ship quickly, the first iteration should include only:

- One input topic: `mm.player.queue`
- Output topics for `mm.match.created` and dequeue/cancellation handling
- One default queue
- Redis-backed authoritative queue state
- A producer web service and a consumer worker deployed separately
- A simple rule set with player-count checks plus time-based skill widening
- Simple HTTP join, leave, and cancel API support

### MVP Matching Rule
For the first version, a candidate group can form a match when it satisfies player-count bounds and stays within the current skill tolerance window:

$$
minPlayers \leq groupSize \leq maxPlayers
$$

and

$$
|skill_i - skill_j| \leq tolerance(waitTime)
$$

where the tolerance expands over time for players who remain in queue longer.

This keeps the design generic while validating the Redis-coordinated, event-driven matchmaking flow.

---

## 11. Iteration Plan

### Phase 1
- Basic join, leave, and cancel handling
- Default queue only
- Redis-backed authoritative state
- Match creation based on player count and simple skill-bracket grouping

### Phase 2
- Attribute updates
- MMR and region-based rules
- Time-based skill tolerance expansion
- Better queue metrics and dashboards

### Phase 3
- Multiple queues and dynamic config
- More advanced worker coordination patterns
- Recovery and replay hardening
- Optional timeout / failed-match events

---

## 12. Risks and Open Questions

### Risks
- Uneven Kafka partitioning could create hot queues for popular region/mode/bracket combinations
- Redis availability or coordination errors could impact queue ownership and match consistency
- Overly strict rules may stall match formation before tolerance widening catches up
- Duplicate events may cause inconsistent queue state if not handled idempotently

### Open Questions
- Will party membership be modeled as separate players or a single grouped entity?
- Should queue ownership be strictly partition-based from day one?
- How should player expiry and max wait time be surfaced to downstream systems?
- What Redis coordination pattern should be preferred for lease ownership and recovery?

---

## 13. Summary

This design proposes a reusable, EC2-hosted matchmaking architecture built around an API Gateway, a producer web service, Kafka, Redis, and consumer workers. Redis is the authoritative queue/state layer, Kafka feeds the event stream, and workers continuously scan pools, widen skill tolerance over time, and emit match lifecycle events for downstream systems.
