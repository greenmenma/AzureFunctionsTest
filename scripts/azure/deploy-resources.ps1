#Requires -Version 7.3
<#
.SYNOPSIS
    発行後の検証に使う Azure リソースを作成する（Flex Consumption の関数アプリ + 必要に応じて各サービス）。

.DESCRIPTION
    作成したリソース名は scripts/azure/.deploy-state.json に保存し、
    configure-after-publish.ps1 / remove-resources.ps1 から参照する。
    すべて 1 つのリソース グループに作るので、検証後はグループごと削除すれば課金が止まる。

.EXAMPLE
    ./deploy-resources.ps1                          # 関数アプリ + ストレージ + Application Insights のみ
    ./deploy-resources.ps1 -ServiceBus -EventHubs   # + Service Bus / Event Hubs
    ./deploy-resources.ps1 -All                     # Service Bus / Event Hubs / Cosmos DB / SignalR をすべて
#>
param(
    [string]$ResourceGroup = "rg-functionstest",
    [string]$Location = "japaneast",
    # グローバルに一意な名前が必要なリソースの接頭辞（英小文字と数字のみ）
    [string]$Prefix = ("fntest" + (Get-Random -Minimum 1000 -Maximum 9999)),
    # Flex Consumption の最大インスタンス数（課金の上限を抑えるため小さめにしている）
    [int]$MaximumInstanceCount = 10,
    [switch]$ServiceBus,
    [switch]$EventHubs,
    [switch]$CosmosDb,
    [switch]$SignalR,
    [switch]$All
)

$ErrorActionPreference = "Stop"
# az が失敗したらスクリプトを止める
$PSNativeCommandUseErrorActionPreference = $true

if ($All) { $ServiceBus = $EventHubs = $CosmosDb = $SignalR = $true }

$statePath = Join-Path $PSScriptRoot ".deploy-state.json"
$names = [ordered]@{
    ResourceGroup = $ResourceGroup
    Location      = $Location
    FunctionApp   = "$Prefix-func"
    Storage       = ($Prefix -replace '[^a-z0-9]', '') + "st"
    ServiceBus    = if ($ServiceBus) { "$Prefix-sb" } else { $null }
    EventHubs     = if ($EventHubs) { "$Prefix-eh" } else { $null }
    CosmosDb      = if ($CosmosDb) { "$Prefix-cosmos" } else { $null }
    SignalR       = if ($SignalR) { "$Prefix-signalr" } else { $null }
}

Write-Host "== 作成するリソース ==" -ForegroundColor Cyan
$names.GetEnumerator() | Where-Object Value | ForEach-Object { Write-Host ("  {0,-14} {1}" -f $_.Key, $_.Value) }

# 拡張機能が必要なコマンド（application-insights 等）を確認なしでインストールする
az config set extension.use_dynamic_install=yes_without_prompt --only-show-errors | Out-Null

Write-Host "`n[1/6] リソース グループ" -ForegroundColor Cyan
az group create -n $ResourceGroup -l $Location --tags purpose=functions-test -o none

Write-Host "[2/6] ストレージ アカウント（AzureWebJobsStorage / StorageConnection）" -ForegroundColor Cyan
az storage account create -n $names.Storage -g $ResourceGroup -l $Location `
    --sku Standard_LRS --allow-blob-public-access false --min-tls-version TLS1_2 -o none
$storageConn = az storage account show-connection-string -n $names.Storage -g $ResourceGroup --query connectionString -o tsv

Write-Host "[3/6] 関数アプリ（Flex Consumption / .NET 10 isolated / Application Insights 付き）" -ForegroundColor Cyan
az functionapp create -n $names.FunctionApp -g $ResourceGroup `
    --storage-account $names.Storage `
    --flexconsumption-location $Location `
    --runtime dotnet-isolated --runtime-version 10.0 `
    --instance-memory 2048 `
    --maximum-instance-count $MaximumInstanceCount -o none

# アプリ設定（local.settings.json は発行されないので、ここで設定する）
$settings = [ordered]@{
    StorageConnection = $storageConn
    TimerSchedule     = "0 */5 * * * *"   # Azure では 5 分間隔（実行回数 = 課金を抑える）
    KafkaBrokerList   = "localhost:9092"  # Kafka は Azure 検証の対象外。バインド式の解決用のダミー値

    # Flex Consumption ではポーリング方式の Blob トリガーは使えない → Event Grid 方式を使う
    "AzureWebJobs.BlobTrigger.Disabled"          = "true"
    "AzureWebJobs.BlobTriggerEventGrid.Disabled" = "false"

    # Flex Consumption で使えない / 今回 Azure に用意しないもの
    "AzureWebJobs.RabbitMQSend.Disabled"    = "true"
    "AzureWebJobs.RabbitMQTrigger.Disabled" = "true"
    "AzureWebJobs.KafkaSend.Disabled"       = "true"
    "AzureWebJobs.KafkaTrigger.Disabled"    = "true"
    "AzureWebJobs.SqlInsertItem.Disabled"   = "true"
    "AzureWebJobs.SqlGetItems.Disabled"     = "true"
    "AzureWebJobs.SqlTriggerItems.Disabled" = "true"
}

$serviceFunctions = @{
    ServiceBus = "ServiceBusSend", "ServiceBusQueueTrigger", "ServiceBusDeadLetterTrigger", "ServiceBusTopicTrigger"
    EventHubs  = "EventHubSend", "EventHubBatchTrigger"
    CosmosDb   = "CosmosDbCreateItem", "CosmosDbGetItem", "CosmosDbChangeFeedTrigger"
    SignalR    = "SignalRNegotiate", "SignalRBroadcast", "SignalRMessagesTrigger", "SignalRConnectionsTrigger"
}
foreach ($svc in $serviceFunctions.Keys) {
    $enabled = [bool]$names[$svc]
    foreach ($fn in $serviceFunctions[$svc]) {
        $settings["AzureWebJobs.$fn.Disabled"] = (-not $enabled).ToString().ToLower()
    }
}

Write-Host "[4/6] メッセージング（Service Bus / Event Hubs）" -ForegroundColor Cyan
if ($ServiceBus) {
    # トピックを使うため Standard が必要（Basic はキューのみ）
    az servicebus namespace create -n $names.ServiceBus -g $ResourceGroup -l $Location --sku Standard -o none
    az servicebus queue create -n sample-queue --namespace-name $names.ServiceBus -g $ResourceGroup --max-delivery-count 3 -o none
    az servicebus topic create -n sample-topic --namespace-name $names.ServiceBus -g $ResourceGroup -o none
    az servicebus topic subscription create -n sample-subscription --topic-name sample-topic `
        --namespace-name $names.ServiceBus -g $ResourceGroup --max-delivery-count 3 -o none
    $settings.ServiceBusConnection = az servicebus namespace authorization-rule keys list `
        --namespace-name $names.ServiceBus -g $ResourceGroup -n RootManageSharedAccessKey --query primaryConnectionString -o tsv
}
if ($EventHubs) {
    # 検証用途なので最安の Basic（コンシューマー グループは $Default のみ）
    az eventhubs namespace create -n $names.EventHubs -g $ResourceGroup -l $Location --sku Basic -o none
    az eventhubs eventhub create -n sample-hub --namespace-name $names.EventHubs -g $ResourceGroup `
        --partition-count 2 --cleanup-policy Delete --retention-time 1 -o none
    $settings.EventHubsConnection = az eventhubs namespace authorization-rule keys list `
        --namespace-name $names.EventHubs -g $ResourceGroup -n RootManageSharedAccessKey --query primaryConnectionString -o tsv
}

Write-Host "[5/6] データ / リアルタイム（Cosmos DB / SignalR）" -ForegroundColor Cyan
if ($CosmosDb) {
    # サーバーレス: 使った RU とストレージだけ課金される（作成に数分かかる）
    az cosmosdb create -n $names.CosmosDb -g $ResourceGroup --locations regionName=$Location `
        --capabilities EnableServerless --default-consistency-level Session -o none
    az cosmosdb sql database create -a $names.CosmosDb -g $ResourceGroup -n SampleDb -o none
    # 変更フィード トリガーは監視対象のコンテナーを作らないため、先に作っておく
    az cosmosdb sql container create -a $names.CosmosDb -g $ResourceGroup -d SampleDb -n Items --partition-key-path /category -o none
    $settings.CosmosDbConnection = az cosmosdb keys list -n $names.CosmosDb -g $ResourceGroup `
        --type connection-strings --query "connectionStrings[0].connectionString" -o tsv
}
if ($SignalR) {
    # Free_F1: 同時接続 20 / 1 日 2 万メッセージまで無料。サーバーレス モード必須
    az signalr create -n $names.SignalR -g $ResourceGroup -l $Location --sku Free_F1 --service-mode Serverless -o none
    $settings.SignalRConnection = az signalr key list -n $names.SignalR -g $ResourceGroup --query primaryConnectionString -o tsv
}

Write-Host "[6/6] アプリ設定の反映" -ForegroundColor Cyan
$settingArgs = $settings.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }
az functionapp config appsettings set -n $names.FunctionApp -g $ResourceGroup --settings @settingArgs -o none

$names | ConvertTo-Json | Set-Content -Path $statePath -Encoding utf8
Write-Host "`n完了しました。リソース名を $statePath に保存しました。" -ForegroundColor Green
Write-Host "次の手順: VS2026 で関数アプリ '$($names.FunctionApp)' に発行し、configure-after-publish.ps1 を実行してください。"
