# LocalStack provisioning

This folder contains the LocalStack-ready CloudFormation template for the producer web service.

## Deploy

Run the PowerShell helper from the workspace root:

pwsh ./scripts/deploy-localstack-producer-web.ps1

You can override the Kafka broker or topic as needed:

pwsh ./scripts/deploy-localstack-producer-web.ps1 -KafkaBootstrapServers "localhost:9092" -KafkaTopicName "mm.player.queue"
