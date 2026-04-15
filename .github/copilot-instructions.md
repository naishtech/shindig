# Workspace coding instructions

## C# structure conventions
- Keep interfaces in dedicated files.
- Keep classes in dedicated files.
- Use generic, capability-based names for interfaces.
- Use specific, concrete names for implementations.
- Do not declare interfaces inside implementation files.
- Do not declare multiple production classes inside a single file.
- Prefer one primary type per file when adding new production code.

## Delivery conventions
- Follow TDD for behavior changes.
- Keep changes small and production-focused.
- Verify with relevant tests before claiming completion.
- Add short descriptions to public classes, interfaces, and methods.
- Method descriptions should briefly state the system state at the time they are called.
- Use dependency injection to assign concrete implementations to interfaces.
- Prefer constructor injection and keep implementation wiring at the composition root.
- Use xUnit for C# automated tests.
- In unit tests, swap interface implementations using mocks.
- Add component tests when validating a logical component in its intended environment.
- For local infrastructure flows in this sample, prefer component tests that exercise Kafka together with LocalStack.
