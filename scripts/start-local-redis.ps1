param(
    [string]$ContainerName = "shindig-redis",
    [string]$Image = "redis:7.4-alpine",
    [int]$RedisPort = 6379
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "Docker is required to provision the local Redis container."
}

$existingContainer = docker ps -a --filter "name=^/${ContainerName}$" --format "{{.Names}}"
$runningContainer = docker ps --filter "name=^/${ContainerName}$" --format "{{.Names}}"

if (-not $existingContainer) {
    docker run -d `
        --name $ContainerName `
        -p "${RedisPort}:6379" `
        $Image `
        redis-server --save "" --appendonly no | Out-Null
}
elseif (-not $runningContainer) {
    docker start $ContainerName | Out-Null
}

$runningContainer = docker ps --filter "name=^/${ContainerName}$" --format "{{.Names}}"

if (-not $runningContainer) {
    throw "Redis container $ContainerName failed to start."
}

Write-Host "Redis container $ContainerName is running on localhost:$RedisPort"
