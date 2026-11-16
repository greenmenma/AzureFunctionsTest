# 発行後（Azure 上）の検証手順

ローカルのエミュレーターで確認した関数アプリを Azure に発行し、Azure 上での挙動を確認する手順です。
ローカルでは確認できない次の点を見ることが目的です。

- 実サービスとの接続（接続文字列 / キー / ネットワーク）
- スケール（インスタンスの増減）とコールド スタート
- リトライ、poison キュー、デッドレターの実際の挙動
- Application Insights でのログ・メトリクス
- 実際にかかった費用

> **費用について**: この手順で作るリソースは、使わなくても課金されるもの（Service Bus Standard、Event Hubs Basic など）を含みます。
> 検証が終わったら、必ず「7. 後片付け」でリソース グループごと削除してください。

---

## 0. 前提

| 必要なもの | 備考 |
|---|---|
| Azure サブスクリプション | リソース グループを作れる権限（共同作成者以上） |
| Azure CLI | `winget install Microsoft.AzureCLI` |
| PowerShell 7.3 以上 | `winget install Microsoft.PowerShell`（スクリプトは `pwsh` で実行する） |
| Visual Studio 2026 | 発行に使う。Azure アカウントでサインインしておく |

```powershell
az login
az account show --query "{name:name, id:id}" -o table   # 対象のサブスクリプションか確認
# 違う場合: az account set --subscription <サブスクリプション ID>
```

## 1. 検証の範囲（ホスティング プラン）

この手順では **Flex Consumption プラン**（従量課金。現在の推奨プラン）に発行します。プランによって使えるトリガーが違うので、Azure で確認できる範囲は次のとおりです。

| トリガー | Azure での検証 | 補足 |
|---|---|---|
| HTTP / Timer / Queue / Table / Durable / MCP / Event Grid | ○ | 追加リソースは不要 |
| Blob | ○ | Flex ではポーリング方式が使えないため **BlobTriggerEventGrid** で確認する（`BlobTrigger` は無効化される） |
| Service Bus / Event Hubs / Cosmos DB / SignalR | ○ | スクリプトのスイッチで作成した場合のみ |
| SQL | △ | Azure SQL を手動で用意する場合のみ（後述） |
| Kafka | × | ブローカー（Confluent Cloud 等）が別途必要なため対象外 |
| RabbitMQ | × | Flex Consumption では使えない（Premium / Dedicated プランが必要） |

## 2. リソースの作成

`scripts/azure/deploy-resources.ps1` で、1 つのリソース グループにまとめて作成します。

```powershell
cd scripts/azure

# 最小構成: 関数アプリ（Flex Consumption）+ ストレージ + Application Insights
pwsh ./deploy-resources.ps1

# 追加サービスも作る場合（必要なものだけ指定）
pwsh ./deploy-resources.ps1 -ServiceBus -EventHubs -CosmosDb -SignalR
# すべて: pwsh ./deploy-resources.ps1 -All
```

| パラメーター | 既定値 | 説明 |
|---|---|---|
| `-ResourceGroup` | `rg-functionstest` | リソース グループ名 |
| `-Location` | `japaneast` | リージョン |
| `-Prefix` | `fntest` + 乱数 4 桁 | リソース名の接頭辞（グローバルに一意である必要がある） |
| `-MaximumInstanceCount` | `10` | Flex の最大インスタンス数。**費用の上限を抑えるため小さめ** |

スクリプトは次のことを行います。

- 各サービスを、検証に十分な**最安の構成**で作る（Service Bus はトピックがあるので Standard、Event Hubs は Basic、Cosmos DB はサーバーレス、SignalR は Free）
- 接続文字列をアプリ設定に入れる（`local.settings.json` は発行されないため）
- 作らなかったサービスの関数を `AzureWebJobs.<関数名>.Disabled = true` で無効にする
- Timer の間隔を 5 分にする（ローカルでは 1 分）
- リソース名を `scripts/azure/.deploy-state.json` に保存する（後続のスクリプトが使う。Git の管理対象外）

> 検証用なので接続文字列（キー）で接続しています。本番では Managed Identity（`<接続名>__fullyQualifiedNamespace` 等）を使ってください。

## 3. Visual Studio 2026 から発行

1. ソリューション エクスプローラーで `FunctionsTest` プロジェクトを右クリック →「発行」
2. ターゲット: **Azure** →「Azure Function App」
3. サブスクリプションを選び、手順 2 で作った関数アプリ（`<Prefix>-func`）を選択 →「完了」
4. 発行プロファイルの画面で次を確認
   - 構成: **Release**（Timer の `RunOnStartup` が無効になる）
   - ターゲット フレームワーク: `net10.0`
5. 「発行」をクリック

発行が終わったら、Azure ポータルで関数アプリを開き、「関数」一覧に関数が表示されることを確認します（表示まで 1〜2 分かかることがあります）。

> コマンドで発行する場合: `func azure functionapp publish <関数アプリ名>`（Azure Functions Core Tools が必要）

## 4. 発行後の設定

拡張機能のシステム キーは**発行してホストが起動した後**に作られます。キーを使う設定は、発行後に次のスクリプトで行います。

```powershell
pwsh ./configure-after-publish.ps1
```

| 処理 | 内容 |
|---|---|
| Blob → Event Grid | ストレージの `uploads` コンテナーの BlobCreated イベントを `BlobTriggerEventGrid` に届けるサブスクリプションを作る |
| SignalR アップストリーム | SignalR Service → 関数アプリ（`/runtime/webhooks/signalr`）の URL を設定する（SignalR を作った場合のみ） |
| `.http` 用のキー | `http/http-client.env.json.user` に接続先 URL・関数キー・Event Grid / MCP のシステム キーを書き出す（Git の管理対象外） |

## 5. 動作確認

### 5-1. `.http` ファイルで呼び出す

1. VS2026 で `http/FunctionsTest.http` を開く
2. エディター右上の環境ドロップダウンで **azure** を選ぶ
3. 各リクエストを送信する（ローカルと同じリクエストがそのまま Azure 向けになる）

### 5-2. トリガー別チェックリスト

ローカルとの差が出やすい点を中心に確認します。ログの確認方法は 5-3 を参照してください。

| 対象 | 操作 | 期待する結果 | Azure で特に見る点 |
|---|---|---|---|
| HTTP | `GET /api/hello` | 200 | 初回応答の遅さ（コールド スタート）と 2 回目以降の差 |
| HTTP（認証） | `GET /api/secure`（`code` なし / あり） | なし: **401**、あり: 200 | ローカルでは確認できない認証レベルの効き方 |
| HTTP（例外） | `GET /api/throw` | 500 | Application Insights の `exceptions` に記録される |
| Timer | 待つだけ | 5 分ごとに `TimerFromSetting` | 複数インスタンスでも **1 回だけ** 実行される（ストレージでロックされる） |
| Queue | `POST /api/enqueue` | `QueueTrigger` → `results/` に Blob | ストレージ エクスプローラー（ポータル）で Blob を確認 |
| Queue（poison） | `sample-queue-sdk` に `fail` を含むメッセージを投入 | 3 回失敗後 `sample-queue-sdk-poison` → `QueuePoisonTrigger` | リトライの間隔と回数 |
| Blob | `PUT /api/blobs/hello.txt` | `BlobTriggerEventGrid` が数秒以内に起動 | ポーリング方式より遅延が小さいこと |
| Table | `POST /api/tables/pk1` → `GET /api/tables/pk1` | 追加した行が返る | |
| Durable | `POST /api/durable/start/FanOutFanInOrchestrator` | `statusQueryGetUri` で `Completed` | 並列のアクティビティが**複数インスタンスに分散**されるか |
| Durable（承認） | 開始 → 30 秒以内 / 以降に承認 | `Approved` / `TimedOut` | |
| Event Grid | `.http` の Event Grid リクエスト（`eventGridKey` 付き） | 202、トリガーが起動 | キーなしでは 401 になる |
| MCP | `.http` の MCP リクエスト（`x-functions-key` 付き） | ツール一覧・実行結果が返る | VS Code 等の MCP クライアントからも接続できる（キーをヘッダーに設定） |
| Service Bus | `POST /api/servicebus`（本文に `fail`） | デッドレター → `ServiceBusDeadLetterTrigger` | ポータルの「Service Bus Explorer」でデッドレターを確認 |
| Event Hubs | `POST /api/eventhubs?count=50` | `EventHubBatchTrigger` がバッチで受信 | 1 回あたりのバッチ件数、パーティション（2）ごとの処理 |
| Cosmos DB | `POST /api/cosmos/books` | 変更フィード トリガーが起動 | `leases` コンテナーが自動作成される |
| SignalR | ブラウザーで `https://<アプリ>.azurewebsites.net/api/signalr/client` | 送信がエコーされる | アップストリーム経由のトリガー呼び出し |

### 5-3. ログの確認

**リアルタイム**: ポータルの関数アプリ →「ログ ストリーム」、または Application Insights →「ライブ メトリック」

**Application Insights（ログ）** で使えるクエリの例:

```kusto
// 関数ごとの実行回数・失敗数・処理時間（直近 1 時間）
requests
| where timestamp > ago(1h)
| summarize 実行回数 = count(), 失敗 = countif(success == false),
            平均ms = avg(duration), p95ms = percentile(duration, 95) by operation_Name
| order by 実行回数 desc
```

```kusto
// ミドルウェアのログ（呼び出しの開始・終了・例外）
traces
| where timestamp > ago(1h) and message has "[Middleware]"
| project timestamp, message, cloud_RoleInstance
| order by timestamp desc
```

```kusto
// 例外の一覧
exceptions
| where timestamp > ago(1h)
| project timestamp, operation_Name, type, outerMessage
| order by timestamp desc
```

```kusto
// インスタンス数の推移（スケールアウトの確認）
requests
| where timestamp > ago(1h)
| summarize インスタンス数 = dcount(cloud_RoleInstance) by bin(timestamp, 1m)
| render timechart
```

### 5-4. スケールの確認（任意）

負荷をかけてインスタンスが増える様子を見る場合は、キューにまとめてメッセージを入れるのが手軽です。

```powershell
# sample-queue-sdk に 500 件投入（Azure CLI）
$st = (Get-Content ./.deploy-state.json | ConvertFrom-Json).Storage
$conn = az storage account show-connection-string -n $st --query connectionString -o tsv
1..500 | ForEach-Object -Parallel {
    az storage message put -q sample-queue-sdk --content "load $_" --connection-string $using:conn -o none
} -ThrottleLimit 20
```

その後、5-3 の「インスタンス数の推移」クエリで増減を確認します。最大インスタンス数は `-MaximumInstanceCount`（既定 10）で頭打ちになります。

## 6. 費用の確認

- **Cost Management**: ポータル →「コストの管理と請求」→「コスト分析」で、スコープをリソース グループ `rg-functionstest` にする。サービス別（リソースの種類別）に表示すると、何にいくらかかったかがわかる。**集計には 8〜24 時間ほどの遅れがある**ので、翌日に確認する。
- **関数アプリ（Flex Consumption）**: 実行回数と「実行時間 × メモリ（GB 秒）」で課金される。5-3 の 1 つ目のクエリで出した実行回数と平均処理時間から、大まかに見積もれる。
- **固定費に注意**: Service Bus Standard、Event Hubs Basic は、**メッセージを流さなくても時間単位で課金**される。Cosmos DB（サーバーレス）、SignalR（Free）、ストレージは使った分だけなので、検証程度ならごく少額。
- 料金は [Azure 料金計算ツール](https://azure.microsoft.com/pricing/calculator/) で、リージョン `Japan East` を選んで確認する。

> 予算アラートを設定しておくと安心です: ポータル →「コストの管理と請求」→「予算」→ リソース グループ単位で金額を設定し、メール通知を有効にする。

## 7. 後片付け

```powershell
pwsh ./remove-resources.ps1      # 確認のためリソース グループ名の入力を求められる
```

リソース グループごと削除するので、作ったリソースの課金はすべて止まります。削除と同時に `.deploy-state.json` と `http/http-client.env.json.user` も消えます。

> Application Insights を含め、すべて同じリソース グループに作っているため、消し忘れは起きません。ポータルの「リソース グループ」一覧で `rg-functionstest` が消えたことを確認してください。

---

## 付録: SQL トリガーを Azure で確認する場合（手動）

1. ポータルで Azure SQL Database を作成する（**無料オファー**を適用すれば、毎月一定量まで無料で使える）
   - 「ネットワーク」で「Azure サービスおよびリソースにこのサーバーへのアクセスを許可する」を **はい** にする
2. クエリ エディターで `emulators/sql/init.sql` の `USE SampleDb;` 以降を実行する（`CREATE DATABASE` の部分は不要）
3. 関数アプリのアプリ設定に次を追加する
   - `SqlConnection`: ポータルの「接続文字列」（ADO.NET）にパスワードを入れたもの
   - `AzureWebJobs.SqlInsertItem.Disabled` / `SqlGetItems` / `SqlTriggerItems`: `false`
4. `.http` の SQL のリクエストで確認する
