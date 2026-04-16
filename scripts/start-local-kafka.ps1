param(
    [string]$ContainerName = "shindig-kafka",
    [string]$Image = "docker.redpanda.com/redpandadata/redpanda:v24.1.10",
    [int]$KafkaPort = 9092,
    [int]$AdminPort = 9644,
    [string]$TopicName = "mm.player.queue"
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "Docker is required to provision the local Kafka container."
}

$existingContainer = docker ps -a --filter "name=^/${ContainerName}$" --format "{{.Names}}"
$runningContainer = docker ps --filter "name=^/${ContainerName}$" --format "{{.Names}}"

if (-not $existingContainer) {
    docker run -d `
        --name $ContainerName `
        -p "${KafkaPort}:9092" `
        -p "${AdminPort}:9644" `
        $Image `
        redpanda start `
        --overprovisioned `
        --smp 1 `
        --memory 1G `
        --reserve-memory 0M `
        --node-id 0 `
        --check=false `
        "--kafka-addr=internal://0.0.0.0:9092,external://0.0.0.0:19092" `
        "--advertise-kafka-addr=internal://127.0.0.1:9092,external://localhost:$KafkaPort" `
        "--pandaproxy-addr=internal://0.0.0.0:8082,external://0.0.0.0:18082" `
        "--advertise-pandaproxy-addr=internal://127.0.0.1:8082,external://localhost:18082" | Out-Null
}
elseif (-not $runningContainer) {
    docker start $ContainerName | Out-Null
}

$runningContainer = docker ps --filter "name=^/${ContainerName}$" --format "{{.Names}}"

if (-not $runningContainer) {
    throw "Kafka container $ContainerName failed to start."
}

Write-Host "Kafka container $ContainerName is running on localhost:$KafkaPort"

$topicResult = docker exec $ContainerName rpk topic create $TopicName --brokers 127.0.0.1:9092 2>&1

if ($LASTEXITCODE -eq 0 -or ($topicResult | Out-String) -match "TOPIC_ALREADY_EXISTS") {
    Write-Host "Kafka topic $TopicName is ready."
}
else {
    Write-Warning "Kafka topic creation could not be confirmed immediately. The broker may still be warming up."
}
