param(
    [string]$StackName = "shindig-matchmaker-producer-local",
    [string]$TemplatePath = "infra/cloudformation/matchmaker-producer-web-localstack.yaml",
    [string]$AwsEndpoint = "http://localhost:4566",
    [string]$EnvironmentName = "local",
    [string]$AppName = "MatchMakerProducerWebService",
    [int]$AppPort = 8080,
    [string]$KafkaBootstrapServers = "host.docker.internal:9092",
    [string]$KafkaTopicName = "mm.player.queue",
    [string]$InstanceType = "t3.small",
    [string]$AmiId = "ami-localstack",
    [string]$LocalStackContainerName = "shindig-localstack"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $TemplatePath)) {
    throw "Template not found at $TemplatePath"
}

$resolvedTemplate = (Resolve-Path $TemplatePath).Path

$parameterOverrides = @(
    "EnvironmentName=$EnvironmentName",
    "AppName=$AppName",
    "AppPort=$AppPort",
    "KafkaBootstrapServers=$KafkaBootstrapServers",
    "KafkaTopicName=$KafkaTopicName",
    "InstanceType=$InstanceType",
    "AmiId=$AmiId"
)

if (Get-Command awslocal -ErrorAction SilentlyContinue) {
    awslocal cloudformation deploy `
        --stack-name $StackName `
        --template-file $resolvedTemplate `
        --capabilities CAPABILITY_NAMED_IAM `
        --parameter-overrides $parameterOverrides

    awslocal cloudformation describe-stacks --stack-name $StackName
    exit $LASTEXITCODE
}

if (Get-Command aws -ErrorAction SilentlyContinue) {
    aws --endpoint-url $AwsEndpoint cloudformation deploy `
        --stack-name $StackName `
        --template-file $resolvedTemplate `
        --capabilities CAPABILITY_NAMED_IAM `
        --parameter-overrides $parameterOverrides

    aws --endpoint-url $AwsEndpoint cloudformation describe-stacks --stack-name $StackName
    exit $LASTEXITCODE
}

if (Get-Command docker -ErrorAction SilentlyContinue) {
    $containerExists = docker ps --filter "name=$LocalStackContainerName" --format "{{.Names}}"

    if ($containerExists) {
        $containerTemplatePath = "/tmp/matchmaker-producer-web-localstack.yaml"
        docker cp $resolvedTemplate "${LocalStackContainerName}:${containerTemplatePath}" | Out-Null

        docker exec $LocalStackContainerName awslocal cloudformation deploy `
            --stack-name $StackName `
            --template-file $containerTemplatePath `
            --capabilities CAPABILITY_NAMED_IAM `
            --parameter-overrides $parameterOverrides

        if ($LASTEXITCODE -ne 0) {
            exit $LASTEXITCODE
        }

        docker exec $LocalStackContainerName awslocal cloudformation describe-stacks --stack-name $StackName
        exit $LASTEXITCODE
    }
}

throw "No usable CloudFormation client was found. Install awslocal or aws CLI, or run the LocalStack container named $LocalStackContainerName."
