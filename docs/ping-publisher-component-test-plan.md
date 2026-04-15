# Ping Publisher Local Component Test Plan

## Purpose

Component tests validate a logical component in its intended environment.
For this sample, the ping publisher should be tested against a local Kafka broker while LocalStack provides the local AWS-style infrastructure boundary provisioned through CloudFormation.

## Goals

- prove the ping publisher can publish to the expected Kafka topic locally
- validate that the published message matches the agreed event contract
- confirm the component behaves correctly with the same style of configuration used by higher environments
- surface integration failures such as bad topic names, unreachable brokers, or missing local infrastructure

## Intended Local Environment

### LocalStack responsibilities
- emulate the local AWS-facing environment
- host Lambda-style execution and related local infrastructure concerns
- provision the local infrastructure stack through CloudFormation
- provide a repeatable local setup for component testing

### CloudFormation responsibilities
- define the local infrastructure declaratively
- create the Lambda-related resources in LocalStack
- keep the local environment close to higher-environment deployment patterns
- make test setup reproducible across developers and CI

### Kafka responsibilities
- run as the real message broker used by the component test
- expose the ping topic for producer and consumer verification
- allow the test to assert that a message was actually published

## Component Architecture

The local component test should reflect the intended runtime shape of the infrastructure slice.
In this design, the ping publisher runs in the LocalStack-backed environment and publishes to Kafka hosted on an EC2-style instance.

### Architecture Summary
- LocalStack hosts the AWS-style resources provisioned through CloudFormation
- the ping publisher runs as the logical component under test
- Kafka runs on an EC2 instance and exposes the infrastructure ping topic
- the component test invokes the publisher and then verifies the emitted message on Kafka

### Logical Flow
1. CloudFormation provisions the LocalStack resources needed by the ping flow
2. the ping publisher starts with the local environment configuration
3. the publisher opens a connection to Kafka on the EC2 instance
4. the publisher emits an infrastructure ping event to the configured topic
5. a verification consumer reads the message from Kafka and asserts the event contract

### Deployment Boundary
This keeps the test realistic by separating the AWS-style execution environment from the broker runtime:
- LocalStack represents the serverless and infrastructure control plane
- Kafka on an EC2 instance represents the external messaging platform dependency
- the component test validates the actual integration point between them

## Proposed Test Scope

The component test should cover the full ping publishing flow for the logical component:

1. construct the ping message
2. connect to the configured Kafka broker
3. publish to the infrastructure ping topic
4. consume the message from Kafka
5. verify the payload fields and publish outcome

## Recommended Test Cases

### 1. Successful publish
- start LocalStack and Kafka locally
- invoke the ping publisher with the local environment configuration
- verify one message is published to the infrastructure ping topic
- assert the message includes the expected event type, timestamp, source, environment, correlation identifier, and metadata

### 2. Topic and environment configuration
- run the component with an explicit local topic name and environment name
- verify the message is published to the configured topic
- verify the environment field matches the local test setup

### 3. Broker unavailable failure path
- stop Kafka or point the component to an invalid broker address
- invoke the component
- verify the failure is surfaced clearly through the result or logs

### 4. Missing topic or provisioning issue
- run the component before the topic is created, if auto-create is disabled
- verify the failure mode is visible and diagnosable

### 5. Repeated invocation behavior
- invoke the publisher multiple times in the same local environment
- verify each message reaches Kafka and carries a distinct correlation identifier

## Test Harness Plan

### Test project structure
Use a dedicated component test area so the local environment requirements stay separate from unit tests.

Suggested direction:
- keep fast unit tests in the current test project
- add a component test suite for local environment verification
- mark component tests clearly so they can be run on demand or in CI profiles that support Docker

### Local dependency startup
Use the following approach for this sample:
- Docker Compose to bring up LocalStack and Kafka before the test run
- CloudFormation templates deployed to LocalStack to provision the AWS-side resources
- optional Testcontainers later if the team wants fully self-contained test orchestration from code

CloudFormation should be the primary provisioning model because it is fully supported in LocalStack and keeps the sample aligned with real deployment practices.

## Execution Flow

1. start LocalStack and Kafka
2. deploy the local CloudFormation stack into LocalStack
3. create or verify the ping topic
4. deploy or configure the ping publisher for the local environment
5. invoke the publisher
6. consume from Kafka and assert the message contract
7. collect logs for diagnostics
8. tear down local resources or delete the local stack

## Validation Criteria

The component test is successful when:

- the publish operation completes against the local broker
- the message is observable on the Kafka topic
- the payload matches the technical design
- failures produce actionable diagnostics

## Follow-up Implementation Steps

1. add a CloudFormation template for the LocalStack-backed infrastructure used by the ping flow
2. add local startup scripts for Kafka and LocalStack
3. add a deployment step such as awslocal cloudformation deploy for the local stack
4. create a dedicated component test suite in the test area
5. add a Kafka consumer assertion helper for end-to-end verification
6. wire the component tests into a local developer workflow and an optional CI job
