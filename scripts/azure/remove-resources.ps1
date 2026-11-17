#Requires -Version 5.1
<#
.SYNOPSIS
    検証用のリソースを削除する（課金を止める）。

.DESCRIPTION
    deploy-resources.ps1 がリソース グループを作った場合は、グループごと削除する。
    既存のリソース グループ（管理者が用意したもの等）を使った場合は、グループは残し、
    中のリソースだけをすべて削除する（グループの削除には権限がないことが多いため）。
#>
param(
    # 確認プロンプトを出さずに削除する
    [switch]$Force
)

# 共通処理（az 失敗時の停止など。Windows PowerShell 5.1 / PowerShell 7 両対応）
. (Join-Path $PSScriptRoot "_common.ps1")

$statePath = Join-Path $PSScriptRoot ".deploy-state.json"
if (-not (Test-Path $statePath)) { throw "$statePath がありません。削除対象のリソース グループ名を確認できません。" }
$s = Get-Content $statePath -Raw | ConvertFrom-Json
$rg = $s.ResourceGroup
# 古い .deploy-state.json には CreatedResourceGroup がない → 従来どおりグループごと削除
$deleteGroup = $s.CreatedResourceGroup -ne $false

Write-Host "リソース グループ '$rg' 内のリソース:" -ForegroundColor Yellow
az resource list -g $rg --query "[].{name:name, type:type}" -o table

if (-not $Force) {
    $target = if ($deleteGroup) { "リソース グループ '$rg' を" } else { "'$rg' 内の上記リソースをすべて" }
    $answer = Read-Host "`n${target}削除します。元に戻せません。よろしければリソース グループ名を入力してください"
    if ($answer -ne $rg) { Write-Host "中止しました。"; return }
}

if ($deleteGroup) {
    az group delete -n $rg --yes --no-wait
    Write-Host "削除を開始しました（完了まで数分かかります）。進捗: az group show -n $rg" -ForegroundColor Green
} else {
    # 空のテンプレートを Complete モードでデプロイすると、テンプレートにないリソース（＝全部）が削除される。
    # 依存関係の順序は ARM が解決してくれるので、1 件ずつ消すより確実
    $template = Join-Path ([IO.Path]::GetTempPath()) "functionstest-empty.json"
    Write-Utf8File $template '{"$schema":"https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#","contentVersion":"1.0.0.0","resources":[]}'
    Write-Host "グループ内のリソースを削除しています（数分かかります）..." -ForegroundColor Cyan
    try {
        az deployment group create -g $rg -n remove-functionstest --mode Complete --template-file $template -o none
    } finally {
        Remove-Item $template -ErrorAction SilentlyContinue
    }
    Write-Host "削除しました（リソース グループ '$rg' は残っています）。" -ForegroundColor Green
    az resource list -g $rg --query "[].{name:name, type:type}" -o table
}

Remove-Item $statePath
$envUser = Join-Path $PSScriptRoot "../../http/http-client.env.json.user"
if (Test-Path $envUser) { Remove-Item $envUser }
