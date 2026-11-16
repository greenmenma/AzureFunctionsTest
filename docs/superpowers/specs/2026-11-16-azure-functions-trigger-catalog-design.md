# Azure Functions トリガー網羅検証プロジェクト 設計

## 目的
Azure Functions（関数アプリ）の主要なトリガー／バインドを一通り動かし、挙動を確認できる検証用プロジェクトを用意する。Windows の Visual Studio 2026 でブレークポイントを張ってデバッグできることが前提。

## ゴール / 非ゴール
- ゴール: 主要トリガーを網羅し、1 ソリューションで F5 起動・個別に動作確認できること
- ゴール: ローカルのエミュレーターだけで完結して検証できること（Azure 実リソースは任意で差し替え可能）
- 非ゴール: 本番品質の業務ロジック、CI/CD、IaC（Bicep 等）によるデプロイ自動化
- アンチゴール: 一部のエミュレーターが起動していないだけで、関数アプリ全体が起動しなくなること

## 制約
- .NET 10（LTS）/ Azure Functions v4 / isolated worker モデル（in-process は 2026/11/10 でサポート終了）
- NuGet パッケージは作成時点の最新安定版
- VS2026（Azure 開発ワークロード）でデバッグ

## 検討した方式
| 案 | 内容 | 判断 |
|---|---|---|
| A（採用） | .NET 10 + VS2026 | 最新版でサポート期間も長い。ユーザーが選択 |
| B | .NET 8 + VS2022 | サポート終了済みのため不採用 |

プロジェクト構成は「1 つの関数アプリにトリガー種別ごとのフォルダーを分けて置く」方式にする（Claude の判断）。トリガーごとに関数アプリを分ける方式は、F5 の手間とポートの競合が増えるため採らない。

## 構成
```
AzureFunctionsTest.slnx
src/FunctionsTest/            … 関数アプリ本体（net10.0, isolated, ASP.NET Core 統合）
  Functions/Http/             … GET/POST、ルートパラメーター、認証レベル、複数出力
  Functions/Timer/            … CRON、RunOnStartup
  Functions/Storage/          … Queue（トリガー/出力/ポイズン）、Blob（トリガー/入力/出力）、Table（入力/出力）
  Functions/ServiceBus/       … キュー、トピック/サブスクリプション、メッセージアクション（完了/デッドレター）
  Functions/EventHubs/        … バッチ受信、出力
  Functions/EventGrid/        … EventGrid トリガー（CloudEvent / EventGridEvent）
  Functions/CosmosDb/         … 変更フィード トリガー、入力、出力
  Functions/Durable/          … チェーン、ファンアウト/ファンイン、外部イベント（人による承認）、モニター、エンティティ
  Middleware/                 … 呼び出しログ用ミドルウェア（isolated 特有の機能を確認するため）
docker-compose.yml           … Azurite、Service Bus エミュレーター、Event Hubs エミュレーター、Cosmos DB エミュレーター
http/FunctionsTest.http      … VS2026 の HTTP ファイルで各関数を叩けるリクエスト集
README.md                    … セットアップ、起動、トリガーごとの確認手順（日本語）
```

## 前提が揃っていないときの起動方針
`local.settings.json` で `AzureWebJobs.<関数名>.Disabled` を使い、エミュレーターがない関数を個別に無効化できるようにする。初期値は HTTP / Timer / Storage / Durable を有効にし、Service Bus / Event Hubs / Cosmos DB / Event Grid は無効にしておく（Claude の判断。Azurite だけで最低限動くようにするため）。

## 作り手に任せる部分
- 各関数のログ内容、サンプルのペイロード形式
- エミュレーターの設定ファイル（Service Bus の Config.json など）の具体的な内容
