#Requires -Version 7.3
<#
.SYNOPSIS
    発行後にしかできない設定を行い、.http ファイル用のキーを出力する。

.DESCRIPTION
    拡張機能のシステム キー（blobs_extension / signalr_extension 等）は、
    関数アプリを発行してホストが起動した後でないと発行されない。そのため次の設定はここで行う。
      - Blob の BlobCreated イベント → BlobTriggerEventGrid への Event Grid サブスクリプション
      - SignalR Service のアップストリーム URL（SignalR トリガー用）
    最後に http/http-client.env.json.user を生成し、VS2026 の .http から Azure 環境を叩けるようにする。
#>
$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true

$statePath = Join-Path $PSScriptRoot ".deploy-state.json"
if (-not (Test-Path $statePath)) { throw "$statePath がありません。先に deploy-resources.ps1 を実行してください。" }
$s = Get-Content $statePath -Raw | ConvertFrom-Json
$rg = $s.ResourceGroup
$app = $s.FunctionApp

$hostName = az functionapp show -n $app -g $rg --query defaultHostName -o tsv
Write-Host "関数アプリ: https://$hostName" -ForegroundColor Cyan

Write-Host "`n[1/4] キーの取得" -ForegroundColor Cyan
$keys = az functionapp keys list -n $app -g $rg | ConvertFrom-Json
$functionKey = $keys.functionKeys.default
$system = $keys.systemKeys
foreach ($name in "blobs_extension", "eventgrid_extension", "mcp_extension", "signalr_extension", "durabletask_extension") {
    $state = if ($system.$name) { "あり" } else { "なし（該当する関数が無効、または未発行）" }
    Write-Host ("  {0,-22} {1}" -f $name, $state)
}
if (-not $system.blobs_extension) {
    throw "blobs_extension キーがありません。発行が完了し、関数アプリが起動しているか確認してください（ポータルで関数一覧が表示されるまで待つ）。"
}

Write-Host "[2/4] Blob → Event Grid サブスクリプション（BlobTriggerEventGrid 用）" -ForegroundColor Cyan
$storageId = az storage account show -n $s.Storage -g $rg --query id -o tsv
$topicName = "$($s.Storage)-topic"
$existing = az eventgrid system-topic list -g $rg --query "[?name=='$topicName'].name" -o tsv
if (-not $existing) {
    az eventgrid system-topic create -n $topicName -g $rg -l $s.Location `
        --topic-type Microsoft.Storage.StorageAccounts --source $storageId -o none
}
$blobEndpoint = "https://$hostName/runtime/webhooks/blobs?functionName=Host.Functions.BlobTriggerEventGrid&code=$($system.blobs_extension)"
az eventgrid system-topic event-subscription create -n uploads-to-function -g $rg --system-topic-name $topicName `
    --endpoint $blobEndpoint --endpoint-type webhook `
    --included-event-types Microsoft.Storage.BlobCreated `
    --subject-begins-with /blobServices/default/containers/uploads/ -o none

Write-Host "[3/4] SignalR アップストリーム" -ForegroundColor Cyan
if ($s.SignalR) {
    if (-not $system.signalr_extension) { throw "signalr_extension キーがありません。SignalR 関数が有効か確認してください。" }
    $upstream = "https://$hostName/runtime/webhooks/signalr?code=$($system.signalr_extension)"
    az signalr upstream update -n $s.SignalR -g $rg --template url-template="$upstream" hub-pattern="*" event-pattern="*" category-pattern="*" -o none
    Write-Host "  設定しました"
} else {
    Write-Host "  SignalR は作成されていないためスキップ"
}

Write-Host "[4/4] http/http-client.env.json.user の生成" -ForegroundColor Cyan
$envUserPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../../http/http-client.env.json.user"))
@{
    azure = @{
        host         = "https://$hostName"
        functionKey  = $functionKey
        eventGridKey = $system.eventgrid_extension
        mcpKey       = $system.mcp_extension
    }
} | ConvertTo-Json -Depth 3 | Set-Content -Path $envUserPath -Encoding utf8
Write-Host "  $envUserPath（.gitignore 済み。キーを含むのでコミットしないこと）"

Write-Host "`n完了しました。VS2026 で http/FunctionsTest.http を開き、環境を 'azure' に切り替えて実行してください。" -ForegroundColor Green
