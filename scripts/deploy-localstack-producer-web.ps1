param(
    [string]$StackName = "shindig-matchmaker-producer-local",
    [string]$TemplatePath = "infra/cloudformation/matchmaker-producer-web-localstack.yaml",
    [string]$AwsEndpoint = "http://localhost:4566",
    [string]$EnvironmentName = "local",
    [string]$AppName = "MatchMakerProducerWebService",
    [int]$AppPort = 8080,
    [string]$KafkaBootstrapServers = "localhost:9092",
    [string]$KafkaTopicName = "mm.player.queue",
    [string]$KafkaContainerName = "shindig-kafka",
    [string]$KafkaImage = "docker.redpanda.com/redpandadata/redpanda:v24.1.10",
    [int]$KafkaAdminPort = 9644,
    [string]$RedisEndpoint = "localhost:6379",
    [string]$RedisContainerName = "shindig-redis",
    [string]$RedisImage = "redis:7.4-alpine",
    [int]$RedisPort = 6379,
    [bool]$RecreateStack = $true,
    [string]$InstanceType = "t3.small",
    [string]$AmiId = "ami-localstack",
    [string]$LocalStackContainerName = "shindig-localstack"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $TemplatePath)) {
    throw "Template not found at $TemplatePath"
}

$resolvedTemplate = (Resolve-Path $TemplatePath).Path
$kafkaProvisionScript = Join-Path $PSScriptRoot "start-local-kafka.ps1"
$redisProvisionScript = Join-Path $PSScriptRoot "start-local-redis.ps1"

if (-not (Test-Path $kafkaProvisionScript)) {
    throw "Kafka provision script not found at $kafkaProvisionScript"
}

if (-not (Test-Path $redisProvisionScript)) {
    throw "Redis provision script not found at $redisProvisionScript"
}

& $kafkaProvisionScript `
    -ContainerName $KafkaContainerName `
    -Image $KafkaImage `
    -KafkaPort 9092 `
    -AdminPort $KafkaAdminPort `
    -TopicName $KafkaTopicName

& $redisProvisionScript `
    -ContainerName $RedisContainerName `
    -Image $RedisImage `
    -RedisPort $RedisPort

$parameterOverrides = @(
    "EnvironmentName=$EnvironmentName",
    "AppName=$AppName",
    "AppPort=$AppPort",
    "KafkaBootstrapServers=$KafkaBootstrapServers",
    "KafkaTopicName=$KafkaTopicName",
    "RedisEndpoint=$RedisEndpoint",
    "InstanceType=$InstanceType",
    "AmiId=$AmiId"
)

if (Get-Command awslocal -ErrorAction SilentlyContinue) {
    if ($RecreateStack) {
        awslocal cloudformation describe-stacks --stack-name $StackName 1>$null 2>$null

        if ($LASTEXITCODE -eq 0) {
            awslocal cloudformation delete-stack --stack-name $StackName
            awslocal cloudformation wait stack-delete-complete --stack-name $StackName
        }
    }

    awslocal cloudformation deploy `
        --stack-name $StackName `
        --template-file $resolvedTemplate `
        --capabilities CAPABILITY_NAMED_IAM `
        --parameter-overrides $parameterOverrides

    & $kafkaProvisionScript `
        -ContainerName $KafkaContainerName `
        -Image $KafkaImage `
        -KafkaPort 9092 `
        -AdminPort $KafkaAdminPort `
        -TopicName $KafkaTopicName

    awslocal cloudformation describe-stacks --stack-name $StackName
    exit $LASTEXITCODE
}

if (Get-Command aws -ErrorAction SilentlyContinue) {
    if ($RecreateStack) {
        aws --endpoint-url $AwsEndpoint cloudformation describe-stacks --stack-name $StackName 1>$null 2>$null

        if ($LASTEXITCODE -eq 0) {
            aws --endpoint-url $AwsEndpoint cloudformation delete-stack --stack-name $StackName
            aws --endpoint-url $AwsEndpoint cloudformation wait stack-delete-complete --stack-name $StackName
        }
    }

    aws --endpoint-url $AwsEndpoint cloudformation deploy `
        --stack-name $StackName `
        --template-file $resolvedTemplate `
        --capabilities CAPABILITY_NAMED_IAM `
        --parameter-overrides $parameterOverrides

    & $kafkaProvisionScript `
        -ContainerName $KafkaContainerName `
        -Image $KafkaImage `
        -KafkaPort 9092 `
        -AdminPort $KafkaAdminPort `
        -TopicName $KafkaTopicName

    aws --endpoint-url $AwsEndpoint cloudformation describe-stacks --stack-name $StackName
    exit $LASTEXITCODE
}

if (Get-Command docker -ErrorAction SilentlyContinue) {
    $containerExists = docker ps --filter "name=$LocalStackContainerName" --format "{{.Names}}"

    if ($containerExists) {
        $containerTemplatePath = "/tmp/matchmaker-producer-web-localstack.yaml"
        docker cp $resolvedTemplate "${LocalStackContainerName}:${containerTemplatePath}" | Out-Null

        if ($RecreateStack) {
            docker exec $LocalStackContainerName awslocal cloudformation describe-stacks --stack-name $StackName 1>$null 2>$null

            if ($LASTEXITCODE -eq 0) {
                docker exec $LocalStackContainerName awslocal cloudformation delete-stack --stack-name $StackName
                docker exec $LocalStackContainerName awslocal cloudformation wait stack-delete-complete --stack-name $StackName
            }
        }

        docker exec $LocalStackContainerName awslocal cloudformation deploy `
            --stack-name $StackName `
            --template-file $containerTemplatePath `
            --capabilities CAPABILITY_NAMED_IAM `
            --parameter-overrides $parameterOverrides

        if ($LASTEXITCODE -ne 0) {
            exit $LASTEXITCODE
        }

        & $kafkaProvisionScript `
            -ContainerName $KafkaContainerName `
            -Image $KafkaImage `
            -KafkaPort 9092 `
            -AdminPort $KafkaAdminPort `
            -TopicName $KafkaTopicName

        docker exec $LocalStackContainerName awslocal cloudformation describe-stacks --stack-name $StackName
        exit $LASTEXITCODE
    }
}

throw "No usable CloudFormation client was found. Install awslocal or aws CLI, or run the LocalStack container named $LocalStackContainerName."
