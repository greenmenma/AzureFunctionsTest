#Requires -Version 7.3
<#
.SYNOPSIS
    検証用リソース グループを丸ごと削除する（課金を止める）。
#>
param(
    # 確認プロンプトを出さずに削除する
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true

$statePath = Join-Path $PSScriptRoot ".deploy-state.json"
if (-not (Test-Path $statePath)) { throw "$statePath がありません。削除対象のリソース グループ名を確認できません。" }
$s = Get-Content $statePath -Raw | ConvertFrom-Json
$rg = $s.ResourceGroup

Write-Host "リソース グループ '$rg' 内のリソース:" -ForegroundColor Yellow
az resource list -g $rg --query "[].{name:name, type:type}" -o table

if (-not $Force) {
    $answer = Read-Host "`n'$rg' を削除します。元に戻せません。よろしければリソース グループ名を入力してください"
    if ($answer -ne $rg) { Write-Host "中止しました。"; return }
}

az group delete -n $rg --yes --no-wait
Write-Host "削除を開始しました（完了まで数分かかります）。進捗: az group show -n $rg" -ForegroundColor Green

Remove-Item $statePath
$envUser = Join-Path $PSScriptRoot "../../http/http-client.env.json.user"
if (Test-Path $envUser) { Remove-Item $envUser }
