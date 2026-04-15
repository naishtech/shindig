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

#### 2.1 API Gateway / Ingress
- Accepts HTTP or gRPC requests from game backends or game servers
- Validates incoming payloads
- Translates requests into Kafka events such as join, leave, and update

#### 2.1.1 Local Development Environment with LocalStack
For local development and integration testing, the service should run against a LocalStack environment so engineers can exercise the full event-driven flow without depending on shared cloud infrastructure.

Recommended local setup:
- API Gateway / Ingress running locally
- Matchmaking Service running locally
- LocalStack providing the local infrastructure boundary for messaging and related AWS-style integrations
- Optional Redis container for resilience experiments

This keeps the design cloud-friendly while making the MVP easy to develop and validate on a single machine.

#### 2.2 Matchmaking Service
- Consumes queue events from Kafka
- Maintains queue state in memory or with optional Redis backing
- Runs a periodic matchmaking loop
- Applies rules to determine valid player groups
- Publishes match creation events

#### 2.3 Config Service / Static Config
- Supplies queue and rule configuration
- Can be file-based (YAML/JSON) for MVP
- Can later be moved to a DB-backed or dynamic config topic model

#### 2.4 Downstream Consumers
These are outside the matchmaking service boundary:

- **Session / Server Allocator**: consumes match-created events and provisions runtime resources
- **Notification Service**: informs players of match state
- **Analytics Pipeline**: records queue and match metrics

### Architecture Summary

1. Client-facing systems call the ingress API.
2. The ingress layer publishes queue events to Kafka.
3. The matchmaking service consumes those events and updates queue state.
4. The matchmaker loop evaluates queued players against configured rules.
5. When a valid match is found, the service emits a match-created event.
6. Downstream systems react to that event.

---

## 3. Kafka Topics

### 3.1 Input Topics

#### mm.player.queue
Primary topic for player queue lifecycle events.

Supported event types:
- `PLAYER_JOIN_QUEUE`
- `PLAYER_LEAVE_QUEUE`
- `PLAYER_UPDATE_ATTRIBUTES`

#### mm.queue.config (optional)
Used only if queue configuration becomes dynamic at runtime.

Possible uses:
- Add or remove queues
- Change rules without redeploying
- Tune parameters such as player limits or wait expansion

### 3.2 Output Topics

#### mm.match.created
Published whenever a valid match is formed.

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
    "partySize": 1
  },
  "metadata": {
    "gameId": "game-xyz",
    "modeId": "mode-duo"
  }
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

### 5.1 Queue Manager

#### Responsibilities
- Maintain player state per queue
- Handle add, remove, and update operations
- Keep queue ordering stable by join time
- Support efficient lookup by player ID

#### Suggested Structures
- `Map<queueName, QueueState>` for queues
- `Map<playerId, PlayerEntry>` for direct player access
- Ordered list or deque for waiting players

#### Event Handling
- **PLAYER_JOIN_QUEUE** → add player to queue
- **PLAYER_LEAVE_QUEUE** → remove player from queue
- **PLAYER_UPDATE_ATTRIBUTES** → mutate player attributes in place

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
- Periodically evaluate active queues
- Select candidate groups within configured size limits
- Pass groups through the rule engine
- Emit events for successful matches
- Remove matched players from the queue

#### Pseudocode
```ts
for (const queue of queues) {
  const players = queue.waitingPlayers;

  while (players.length >= queue.minPlayers) {
    const candidateGroup = players.slice(0, queue.maxPlayers);
    const validGroup = ruleEngine.apply(candidateGroup, queue.config, now);

    if (validGroup) {
      createMatch(validGroup, queue);
      removeFromQueue(validGroup, queue);
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
- number of active queues

---

## 6. Minimal API Surface

Although Kafka is the system backbone, a small ingress API improves adoption and integration.

### Endpoints
- `POST /matchmaking/join`
- `POST /matchmaking/leave`
- `POST /matchmaking/update`

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
2. The ingress service validates and publishes `PLAYER_JOIN_QUEUE`.
3. The matchmaking service consumes the event.
4. The queue manager inserts the player into the target queue.
5. The matchmaker loop evaluates the queue during the next tick.
6. If a valid group is found, the service emits `MATCH_CREATED`.

### Leave Flow
1. A leave request is submitted.
2. A `PLAYER_LEAVE_QUEUE` event is published.
3. The queue manager removes the player from the queue if present.

### Update Flow
1. A player attribute update is submitted.
2. A `PLAYER_UPDATE_ATTRIBUTES` event is published.
3. The queue manager updates the stored entry.
4. The new values are considered in future matching passes.

---

## 8. Non-Functional Considerations

### Scalability
- Partition `mm.player.queue` by `queueName` or `playerId`
- Run multiple service instances in the same consumer group
- Keep queue ownership deterministic to avoid duplicate matching

### State Storage
- Start with in-memory state for simplicity and speed
- Optionally add Redis for crash recovery and resilience
- If Redis is used, keep the in-memory copy as the fast working set

### Reliability
- Use idempotent handling for duplicate queue events
- Prevent the same player from existing in the same queue more than once
- Ensure match emission and queue removal are coordinated to avoid duplicate matches
- Validate the end-to-end join-to-match event flow locally through LocalStack before promoting changes to shared environments

### Local Development and Testing
- Use LocalStack as the default local infrastructure dependency for development and integration testing
- Provision local topics and related resources through startup scripts so the environment is reproducible
- Run smoke tests against the local stack to verify join, leave, update, and match-created flows
- Keep application configuration environment-driven so switching between LocalStack and higher environments is low risk

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
- One service deployment
- One default queue
- One Kafka consumer group
- In-memory queue state only
- One LocalStack-backed local environment for developer setup and CI-friendly integration checks

### Future Horizontal Scaling
To scale safely:
- Route all events for a given queue to the same partition or shard owner
- Ensure one active consumer instance owns a queue partition at a time
- Avoid split-brain queue state across instances

This lets the service stay simple while preserving deterministic match decisions.

---

## 10. MVP Scope

To ship quickly, the first iteration should include only:

- One input topic: `mm.player.queue`
- One output topic: `mm.match.created`
- One queue: `default`
- One simple rule set: only `minPlayers` and `maxPlayers`
- In-memory queue manager
- Simple HTTP join and leave API

### MVP Matching Rule
For the first version, any first group of players that satisfies:

$$
minPlayers \leq groupSize \leq maxPlayers
$$

can form a match.

This keeps the design generic and validates the event-driven architecture before adding complexity.

---

## 11. Iteration Plan

### Phase 1
- Basic join / leave handling
- Default queue only
- Match creation based on player count only
- LocalStack-based developer environment and smoke-test flow

### Phase 2
- Attribute updates
- MMR and region-based rules
- Better queue metrics and dashboards

### Phase 3
- Multiple queues and dynamic config
- Time-based rule relaxation
- Redis-backed resilience
- Optional timeout / failed-match events

---

## 12. Risks and Open Questions

### Risks
- Uneven Kafka partitioning could create hot queues
- In-memory-only state can be lost during process restarts
- Overly strict rules may stall match formation
- Duplicate events may cause inconsistent queue state if not handled idempotently

### Open Questions
- Will party membership be modeled as separate players or a single grouped entity?
- Should queue ownership be strictly partition-based from day one?
- How should player expiry and max wait time be surfaced to downstream systems?
- Is Redis required for the first production release or only after proving load?

---

## 13. Summary

This design proposes a reusable and generic matchmaking service that is event-driven, rule-configurable, and easy to extend. The MVP intentionally focuses on a narrow feature set so the system can be validated quickly, then expanded with richer rules, multiple queues, and stronger resilience over time.
