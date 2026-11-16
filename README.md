# Azure Functions トリガー網羅検証プロジェクト

Azure Functions（関数アプリ）の主なトリガー / バインドを一通り動かして、挙動を確かめるためのプロジェクトです。

- .NET 10（LTS）/ Azure Functions v4 / isolated worker モデル
- HTTP は ASP.NET Core 統合（`HttpRequest` / `IActionResult`）
- Visual Studio 2026 で F5 デバッグできます

## 必要なもの（Windows）

| ツール | 用途 |
|---|---|
| Visual Studio 2026 + 「Azure 開発」ワークロード | ビルド・デバッグ（Functions Core Tools は VS が自動で取得します） |
| .NET 10 SDK | VS2026 に同梱 |
| Docker Desktop | エミュレーターの起動 |
| （任意）Azure Storage Explorer | Queue / Blob / Table の中身を確認・直接投入 |

## 起動手順

1. エミュレーターを起動します（最小構成は Azurite だけ）

   ```powershell
   docker compose up -d                        # Azurite のみ
   docker compose --profile all up -d          # すべて（Service Bus / Event Hubs / Cosmos DB も起動）
   ```

   > VS2026 は Functions プロジェクトのデバッグ時に Azurite を自動で起動しようとします。
   > Docker の Azurite とポート（10000〜10002）がぶつかるため、どちらか一方だけを使ってください。
   > VS 側を使う場合は `docker compose up` を省略し、Service Bus 等は `--profile servicebus` のように個別に起動します
   > （Event Hubs エミュレーターだけは Docker 内の Azurite を必要とします）。

2. `AzureFunctionsTest.slnx` を VS2026 で開き、F5 で起動します（ポート 7071）
3. `http/FunctionsTest.http` を開き、各リクエストを送信して動作を確認します

## 関数一覧

| フォルダー | 関数 | トリガー / バインド | 確認できること |
|---|---|---|---|
| Http | HttpHello / HttpRouteParameter / HttpPostJson / HttpFunctionKey / HttpThrow | HTTP | クエリ、ルート制約、JSON 本文と 400 応答、認証レベル、例外時の 500 |
| Http | HttpToQueue | HTTP → Queue 出力 | 複数出力（HTTP 応答 + Queue） |
| Timer | TimerFromSetting / TimerHourly | Timer | アプリ設定から CRON 式を読む、RunOnStartup |
| Storage | QueueTrigger | Queue → Blob 出力 | POCO で受け取る、トリガーのメタデータ、連鎖処理 |
| Storage | QueueTriggerSdkType / QueuePoisonTrigger | Queue | SDK 型 `QueueMessage`、リトライと poison キュー |
| Storage | BlobUpload / BlobTrigger / BlobRead / BlobList | Blob | Blob 出力、Blob トリガー、`BlobClient` / `BlobContainerClient` 入力 |
| Storage | TableInsert / TableGetOne / TableQuery | Table | Table 出力、1 件入力、`TableClient` 入力 |
| ServiceBus | ServiceBusSend / ServiceBusQueueTrigger / ServiceBusDeadLetterTrigger / ServiceBusTopicTrigger | Service Bus | キューとトピックへの同時送信、手動 settle（Complete / DeadLetter）、デッドレターの監視、バッチ受信 |
| EventHubs | EventHubSend / EventHubBatchTrigger | Event Hubs | 複数イベントの送信、`EventData[]` でのバッチ受信 |
| EventGrid | EventGridCloudEventTrigger / EventGridSchemaTrigger | Event Grid | CloudEvents スキーマと Event Grid スキーマ |
| CosmosDb | CosmosDbCreateItem / CosmosDbGetItem / CosmosDbChangeFeedTrigger | Cosmos DB | 出力（自動作成）、入力、変更フィード |
| Sql | SqlInsertItem / SqlGetItems / SqlTriggerItems | Azure SQL / SQL Server | upsert 出力、パラメーター付きクエリ入力、変更の追跡によるトリガー |
| Kafka | KafkaSend / KafkaTrigger | Kafka | 送信、バッチ受信（※ Premium / Dedicated / Flex のみ） |
| RabbitMQ | RabbitMQSend / RabbitMQTrigger | RabbitMQ | 送信、POCO で受信（※ Premium / Dedicated のみ） |
| SignalR | SignalRNegotiate / SignalRBroadcast / SignalRMessagesTrigger / SignalRConnectionsTrigger / SignalRClientPage | SignalR Service | negotiate、ブロードキャスト、クライアント→サーバー呼び出しのトリガー、接続イベント、ブラウザー クライアント |
| Mcp | McpGetTime / McpAdd / McpReadmeResource / McpReviewPrompt | MCP | AI エージェントから呼べるツール、リソース、プロンプト |
| Durable | Chaining / FanOutFanIn / Approval / Monitor / Parent Orchestrator、CounterEntity | Durable Functions | チェーン、並列実行とリトライ、外部イベントとタイムアウト、耐久タイマー、サブオーケストレーション、エンティティ |
| Middleware | InvocationLoggingMiddleware | （全関数） | isolated のミドルウェア（呼び出しの前後と例外のログ） |

## 関数の有効化 / 無効化

最初は Azurite だけで動くように、次の関数を `local.settings.json` で無効にしてあります。

```json
"AzureWebJobs.ServiceBusQueueTrigger.Disabled": "true",
```

使うエミュレーターを起動してから、該当する行を `"false"` に変える（または削除する）と有効になります。

| 対象 | 起動するプロファイル | 無効化を解除する関数 |
|---|---|---|
| Service Bus | `--profile servicebus` | ServiceBus* |
| Event Hubs | `--profile eventhubs` | EventHub* |
| Cosmos DB | `--profile cosmos` | CosmosDb* |
| Event Grid | （不要） | EventGrid* |
| SQL | `--profile sql` | Sql* |
| Kafka | `--profile kafka` | Kafka* |
| RabbitMQ | `--profile rabbitmq` | RabbitMQ* |
| SignalR | （dotnet tool。下記参照） | SignalR*（SignalRClientPage は常に有効） |
| MCP | （不要） | 最初から有効 |

## 各エミュレーターの補足

- **Service Bus**: エンティティは `emulators/servicebus/Config.json` で定義します（`sample-queue`、`sample-topic` / `sample-subscription`、MaxDeliveryCount は 3）。起動には 1 分ほどかかります。
- **Event Hubs**: エンティティは `emulators/eventhubs/Config.json` で定義します（`sample-hub`、2 パーティション）。Service Bus エミュレーターと AMQP ポートが重ならないよう、ホスト側は **5673** にしてあります（接続文字列も `localhost:5673`）。
- **Cosmos DB**: Linux 版エミュレーター（vNext preview、HTTP モード）を使います。データ エクスプローラーは http://localhost:1234 です。最初に `POST /api/cosmos/{category}` を 1 回呼ぶと、DB とコンテナーが自動で作られます。変更フィード トリガーは監視対象のコンテナーを作らないので、作成後にホストを再起動してください。
  Windows ネイティブ版の [Azure Cosmos DB Emulator](https://learn.microsoft.com/azure/cosmos-db/emulator) を使う場合は `--profile cosmos` を起動せず、接続文字列の `http` を `https` に変えます。
- **Event Grid**: 公式のエミュレーターはありません。`http/FunctionsTest.http` の末尾にあるように、ローカルの Webhook エンドポイント（`/runtime/webhooks/EventGrid?functionName=...`）へ直接 POST して確認します。

- **SQL**: `--profile sql` で SQL Server を起動すると、`emulators/sql/init.sql` が自動で実行されます。`SampleDb` と `dbo.Items` が作られ、変更の追跡（Change Tracking）が有効になります。Service Bus エミュレーターと同じ SQL Server を共用しています。
- **Kafka**: `apache/kafka` のシングルノード（`localhost:9092`）です。トピックは最初の送信で自動作成されます。
- **RabbitMQ**: Service Bus と AMQP ポートが重ならないよう、ホスト側は **5674** にしてあります。管理画面は http://localhost:15672（guest / guest）です。
- **SignalR**: SignalR Service のローカル エミュレーターを dotnet tool として入れて、`emulators/signalr` で起動します（`settings.json` にアップストリーム URL が設定済みです）。

  ```powershell
  dotnet tool install -g Microsoft.Azure.SignalR.Emulator
  cd emulators/signalr
  asrs-emulator start          # http://localhost:8888
  ```

  関数アプリを起動したら、ブラウザーで http://localhost:7071/api/signalr/client を開きます。送信するとトリガー経由でエコーされ、`POST /api/signalr/broadcast` を送るとブロードキャストが届きます。
- **MCP**: 追加のサービスは不要です。`.vscode/mcp.json` に接続先（`http://localhost:7071/runtime/webhooks/mcp`）を書いてあるので、VS Code の GitHub Copilot（エージェント モード）などからツールを呼べます。Azure 上では、システム キー `mcp_extension` をヘッダー `x-functions-key` に付ける必要があります。

## Azure に発行して検証する場合

Azure（Flex Consumption プラン）への発行、発行後の動作確認、費用の確認、後片付けまでの手順は **[docs/azure-verification.md](docs/azure-verification.md)** にまとめています。リソースの作成・設定・削除は `scripts/azure/` の PowerShell スクリプトで行えます。

`http/FunctionsTest.http` は、VS2026 の環境ドロップダウンで `local` / `azure` を切り替えると、同じリクエストをローカルにも Azure にも送れます。

## 構成

```
AzureFunctionsTest.slnx
nuget.config                  … フィードを nuget.org のみに固定
docker-compose.yml            … エミュレーター
emulators/                    … Service Bus / Event Hubs / SignalR エミュレーターの定義、SQL 初期化スクリプト
http/FunctionsTest.http       … 動作確認用のリクエスト集
http/http-client.env.json     … .http の接続先（local / azure）
scripts/azure/                … Azure リソースの作成・発行後の設定・削除スクリプト
.vscode/mcp.json              … MCP クライアントの接続設定
src/FunctionsTest/
  Program.cs / host.json / local.settings.json
  Functions/<トリガー種別>/    … 各関数
  Middleware/                 … isolated のミドルウェア
  Models/                     … 共通のペイロード
docs/azure-verification.md    … 発行後（Azure 上）の検証手順
docs/superpowers/             … 設計書・実装計画
```
