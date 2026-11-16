# Azure Functions トリガー網羅検証プロジェクト 実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 主要トリガーを網羅した .NET 10 isolated の関数アプリを、VS2026 で F5 デバッグできる形で作る。
**Architecture:** 1 ソリューション・1 関数アプリ。トリガー種別ごとに `Functions/<種別>/` フォルダーを分ける。ローカルのエミュレーターは docker-compose で起動する。
**Tech Stack:** .NET 10、Azure Functions v4、Worker 2.52.0 / Worker.Sdk 2.1.0、各 Extensions の最新安定版
**Spec:** `docs/superpowers/specs/2026-11-16-azure-functions-trigger-catalog-design.md`

## Global Constraints
- TargetFramework `net10.0`、`AzureFunctionsVersion` `v4`、`OutputType` `Exe`
- コメント・README は日本語、識別子は英語
- エミュレーターがなくても、Azurite だけで F5 起動できること

## Review Focus
- 不正な JSON を POST したとき → 500 ではなく 400 を返す
- Queue の処理が失敗し続けたとき → poison キューへ移る（意図的に失敗させる関数で確認）
- Service Bus の処理が失敗したとき → デッドレターへ移る
- Durable の承認待ちで外部イベントが来ないとき → タイムアウト分岐に進む
- 無効化している関数があっても → ホストが起動する

---

### Task 1: ソリューションと関数アプリの骨組み
- `AzureFunctionsTest.slnx`、`src/FunctionsTest/FunctionsTest.csproj`、`Program.cs`、`host.json`、`local.settings.json`、`Properties/launchSettings.json`、`Middleware/InvocationLoggingMiddleware.cs`
- 検証: `dotnet build` が警告・エラーなしで通る

### Task 2: HTTP / Timer
- `Functions/Http/*`、`Functions/Timer/*`
- 検証: ビルドが通る。`http/FunctionsTest.http` にリクエストを追加する

### Task 3: Storage（Queue / Blob / Table）
- `Functions/Storage/*`。poison 確認用の関数を含める

### Task 4: Service Bus / Event Hubs / Event Grid / Cosmos DB
- `Functions/ServiceBus/*`、`Functions/EventHubs/*`、`Functions/EventGrid/*`、`Functions/CosmosDb/*`。初期状態は Disabled にする

### Task 5: Durable Functions
- `Functions/Durable/*`（チェーン、ファンアウト/ファンイン、外部イベント＋タイムアウト、モニター、エンティティ）

### Task 6: エミュレーターとドキュメント
- `docker-compose.yml`、`emulators/servicebus/Config.json`、`emulators/eventhubs/Config.json`、`README.md`
- 検証: `docker compose config` が通る。最後にビルドを通す
