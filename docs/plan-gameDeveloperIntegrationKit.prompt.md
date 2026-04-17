## Plan: Game Developer Integration Kit

Use a backend-first integration model.

The recommended path is:

1. Game client → title backend
2. Title backend → Shindig HTTP API
3. Shindig handles Redis, Kafka, and matching internally
4. When a match forms, Shindig calls back the title backend with the match result
5. The title backend notifies the player and starts session join

This is the easiest approach for game teams because it avoids exposing infrastructure details and keeps secrets off the client.

---

### Why this is the best fit

| Option | Dev effort | Ops burden | Recommendation |
|---|---:|---:|---|
| HTTP API + SDK + webhook callback | Low | Low | Recommended |
| Direct Kafka consumption | High | High | Advanced teams only |
| Direct game client to matchmaking service | Low at first | Risky | Not recommended |

---

### Steps

1. Productize the current HTTP endpoints as the supported public integration surface.
2. Add a small SDK for C# and Unity-oriented teams so queue, update, leave, and cancel are one-liners.
3. Add signed webhook delivery for match results so game teams do not need Kafka to know when a match is ready.
4. Keep Kafka as the internal backbone for platform and analytics consumers.
5. Publish a quickstart and sample backend showing the full loop from queue request to match notification.

---

### Relevant files

- src/Matchmaking.ProducerWebService/Program.cs — current public API entry points
- src/Matchmaking.ProducerWebService/Matchmaking/MatchmakingRequestHandler.cs — queue lifecycle orchestration
- src/Matchmaking.ConsumerWorker/Matchmaking/MatchCreatedEvent.cs — outbound match payload contract
- docs/generic-matchmaking-service-technical-design.md — architecture baseline to align with
- docs/matchmaking-sequence-diagram.mmd — flow to update with the developer callback path

---

### Verification

1. A sample title backend can queue players without Kafka knowledge.
2. Two queued players produce a match and the title backend receives the callback.
3. Existing Kafka-based downstream flow still works for internal services.

---

### Decisions

- Default integration should be server-to-server
- Kafka should stay optional, not required
- Unity and C# teams should be the first-class onboarding path

This is the cleanest way to make the project easy for game developers to adopt.
