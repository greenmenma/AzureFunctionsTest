<#
.SYNOPSIS
    scripts/azure/*.ps1 の共通処理（各スクリプトからドットソースで読み込む）。

.DESCRIPTION
    Windows PowerShell 5.1 と PowerShell 7 の両方で同じように動かすための処理をまとめる。
      - az: 失敗（終了コード 0 以外）したら例外を投げるラッパー
            （$PSNativeCommandUseErrorActionPreference は 7.3 以降でしか効かないため）
      - Write-Utf8File: BOM なし UTF-8 でファイルを書く
            （5.1 の Set-Content -Encoding utf8 は BOM 付きになるため）
#>

$ErrorActionPreference = "Stop"

# 本物の az（Windows では az.cmd）。下で同名の関数を定義するので、先にパスを解決しておく
$script:AzExe = (Get-Command az -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1).Source
if (-not $script:AzExe) {
    throw "Azure CLI（az）が見つかりません。winget install Microsoft.AzureCLI でインストールし、ターミナルを開き直してください。"
}

# Windows の az は az.cmd（バッチ ファイル）なので、& | < > ^ を含む引数は cmd に解釈されて壊れる
$script:AzIsBatch = $script:AzExe -match '\.(cmd|bat)$'

function az {
    $azArgs = foreach ($a in $args) {
        if ($script:AzIsBatch -and $a -is [string] -and $a -match '[&|<>^]' -and $a -notmatch '[\s"]') {
            # 空白を含まない引数は PowerShell が引用符を付けないので、自分で付けて cmd から保護する
            '"' + $a + '"'
        } else {
            $a
        }
    }
    & $script:AzExe @azArgs
    if ($LASTEXITCODE -ne 0) {
        # 接続文字列などの秘密情報が出ないよう、サブコマンド名までしか表示しない
        $cmd = ($args | Select-Object -First 3) -join " "
        # 例外の「発生場所」はこのファイルになってしまうため、呼び出し元のスクリプトと行番号を添える
        $caller = (Get-PSCallStack)[1]
        $where = if ($caller.ScriptName) { "{0} の {1} 行目" -f (Split-Path $caller.ScriptName -Leaf), $caller.ScriptLineNumber } else { "対話入力" }
        throw "az $cmd ... が失敗しました（終了コード $LASTEXITCODE、$where）。原因はこの上に表示されている az の ERROR: 行を確認してください。"
    }
}

function Write-Utf8File([string]$Path, [string]$Content) {
    [IO.File]::WriteAllText($Path, $Content, (New-Object System.Text.UTF8Encoding($false)))
}
