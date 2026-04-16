# LocalStack provisioning

This folder contains the LocalStack-ready CloudFormation template for the producer web service.

The local deployment flow now also provisions a Kafka-compatible Docker container for local matchmaking traffic before the CloudFormation stack is applied.

## Deploy

Run the PowerShell helper from the workspace root:

pwsh ./scripts/deploy-localstack-producer-web.ps1

This will:
- start or reuse the local Kafka container
- create the matchmaking topic when available
- deploy the LocalStack CloudFormation stack for the producer web app

You can override the Kafka broker, image, or topic as needed:

pwsh ./scripts/deploy-localstack-producer-web.ps1 -KafkaBootstrapServers "localhost:9092" -KafkaTopicName "mm.player.queue"
