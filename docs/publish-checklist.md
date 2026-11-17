# 発行後の確認チェックリスト（ポータルのみ）

管理者が作成した既存の関数アプリに、Visual Studio 2026 からこのプロジェクトを発行したときの確認手順です。
**Azure CLI（az コマンド）は使わず、Azure ポータルの画面だけで**確認します。

> `scripts/azure/` のスクリプト（az コマンドを使うもの）と [azure-verification.md](azure-verification.md) は、権限の都合で**検証保留**です。

各手順の「結果」欄に、見えた内容を記入してください。

---

## 正しく発行できた場合の状態

- ポータルの関数アプリ →「概要」の下部（または「関数」）に、**60 個の関数**が表示される
  - `AzureWebJobs.<関数名>.Disabled` で無効にした関数も、「無効」と表示されるだけで一覧には出る
  - アプリ設定が足りない関数（接続文字列がない等）は、一覧には出るが実行時にエラーになる
- 関数が **1 つも表示されない**場合は、アプリ自体が起動できていない（下の手順で原因を探す）

---

## 1. アプリが起動しているか

| # | 確認すること | 場所 | 期待値 | 結果 |
|---|---|---|---|---|
| 1-1 | アプリの状態 | 「概要」→ 状態 | 実行中（Running） | |
| 1-2 | トップ ページ | ブラウザーで `https://<アプリ名>.azurewebsites.net` を開く（「概要」の「既定のドメイン」） | 「Your Functions 4.0 app is up and running」のページが出る | |
| 1-3 | HTTP 関数を直接呼ぶ | ブラウザーで `https://<既定のドメイン>/api/hello?name=test` を開く | `{"message":"Hello, test!"}` が返る（404 なら関数が読み込まれていない） | |

> 1-3 で応答が返るなら関数は動いています。一覧に出ないのはポータルの表示だけの問題なので、「概要」→「再起動」してから、1〜2 分待ってページを再読み込みしてください。
>
> **1-2 は表示されるのに 1-3 が 502 / 503 になる場合**: ホスト（Functions ランタイム）は動いていますが、関数を実行する .NET のワーカー プロセスが起動できていないか、起動直後に異常終了しています。
> Flex Consumption ではコールド スタート直後に一時的に 503 になることもあるので、30 秒ほど待って再読み込みし、それでも同じなら 5 章でエラーの内容を確認します。

## 2. 関数アプリの種類（このプロジェクトに合っているか）

| # | 確認すること | 場所 | 期待値 | 結果 |
|---|---|---|---|---|
| 2-1 | ホスティング プラン | 「概要」→ App Service プラン / 価格レベル | Flex Consumption、Premium、App Service（Dedicated）、Windows の従量課金のいずれか。**Linux の従量課金（Consumption）は .NET 10 に対応していない** | |
| 2-2 | OS | 「概要」→ オペレーティング システム | Windows / Linux（記録だけ） | |
| 2-3 | ランタイム スタック | Flex Consumption: 「設定」→「構成」（ランタイム）<br>それ以外: 「設定」→「構成」→「全般設定」→ スタックの設定 | **.NET**、バージョン **10（isolated / 分離ワーカー）** | |
| 2-4 | Functions のバージョン | 「概要」→ ランタイム バージョン | 4.x（**Flex Consumption では表示されないのが正常**。常に 4.x で動く） | |

**2-3 が .NET 8 などの場合**: いちばん可能性の高い原因です。.NET 10 のプロジェクトを .NET 8 のアプリに発行するとワーカーが起動できず、関数は 1 つも表示されません。
スタックのバージョンを 10 に変更できるなら変更して保存します（権限がなければ管理者に依頼してください）。

## 3. 環境変数（アプリ設定）

場所: 「設定」→「環境変数」→「アプリ設定」

| # | 名前 | 期待値 | 結果 |
|---|---|---|---|
| 3-1 | `FUNCTIONS_WORKER_RUNTIME` | `dotnet-isolated`（**Flex Consumption では存在しないのが正常**。2-3 で設定する） | |
| 3-2 | `FUNCTIONS_EXTENSION_VERSION` | `~4`（Flex Consumption では存在しないのが正常） | |
| 3-3 | `AzureWebJobsStorage`（または `AzureWebJobsStorage__accountName`） | 存在する | |
| 3-4 | `APPLICATIONINSIGHTS_CONNECTION_STRING` | 存在する（ないとログ ストリームに何も出ない。下記 5 を参照） | |
| 3-5 | `WEBSITE_RUN_FROM_PACKAGE` | 存在する場合は値を記録（`1` または URL）。**Flex Consumption では存在しないのが正常** | |

## 4. 発行が本当に反映されたか

| # | 確認すること | 場所 | 期待値 | 結果 |
|---|---|---|---|---|
| 4-1 | VS の発行結果 | VS の「出力」ウィンドウ（表示: 発行） | 最後に「発行が成功しました」/「Publish Succeeded」。発行先 URL がこのアプリ | |
| 4-2 | 発行プロファイル | VS の発行画面 | 構成 = Release、ターゲット フレームワーク = net10.0、配置先がこのアプリ（スロットではない） | |
| 4-3 | デプロイ履歴 | ポータル「デプロイ センター」→「ログ」（Flex Consumption では「デプロイ」の履歴） | 発行した時刻のデプロイが「成功」 | |

## 5. エラーの確認

| # | 確認すること | 場所 | 見るポイント | 結果 |
|---|---|---|---|---|
| 5-1 | 自動診断 | 「問題の診断と解決」→「Functions app down or reporting errors」（関数アプリがダウンしている、またはエラーを報告している） | 赤や黄色の項目。特に「Functions runtime」「Worker」関連のエラー | |
| 5-2 | ログ ストリーム | 「監視」→「ログ ストリーム」 | 「接続されました」の後に何も出ない場合は、3-4 の Application Insights が未設定か、インスタンスが停止中（下記） | |
| 5-3 | Application Insights | 「監視」→「Application Insights」→ リソースを開く →「ログ」で下のクエリを実行 | `Error indexing`、`Worker failed`、`exited with code`、`Microsoft.Azure.WebJobs.Script` などのエラー | |

5-3 のクエリ（直近 1 時間の警告・エラーと例外。右上の時間範囲は「クエリで設定」のままにする）:

```kusto
union traces, exceptions
| where timestamp > ago(1h)
| where severityLevel >= 2 or itemType == "exception"
| project timestamp, itemType, severityLevel, message, outerMessage, innermostMessage
| order by timestamp desc
| take 50
```

結果が 0 件の場合は、条件を外した `traces | where timestamp > ago(1h) | order by timestamp desc | take 50` で、起動時のログ（`Starting Host`、`Worker process started` など）が出ているかを確認します。

> **ログ ストリームに「接続されました」しか出ない理由**: Flex Consumption / 従量課金ではリクエストがないとインスタンスが 0 台になり、ログが出ません。
> ログ ストリームを開いたまま、別タブで 1-2 / 1-3 の URL を開くと、起動時のログが流れます。それでも何も出ない場合は、Application Insights が紐付いていない（3-4）可能性が高いです。

---

## 6. 関数が表示されるようになった後に追加するアプリ設定

このプロジェクトは、次のアプリ設定がないと一部の関数がエラーになります（通常は該当する関数だけがエラーになり、ほかの関数は動きます。ただし関数が 1 つも動かない場合も、切り分けのため先に設定しておくことをおすすめします）。
「設定」→「環境変数」→「アプリ設定」→「+ 追加」で設定し、最後に「適用」を押します。

| 名前 | 値 | 理由 |
|---|---|---|
| `StorageConnection` | `AzureWebJobsStorage` と同じ値 | Queue / Blob / Table 関数の接続先 |
| `TimerSchedule` | `0 */5 * * * *` | Timer の間隔（5 分。実行回数 = 課金を抑える） |
| `KafkaBrokerList` | `localhost:9092` | Kafka は対象外。バインド式を解決するためのダミー値 |

`AzureWebJobsStorage` がなく `AzureWebJobsStorage__accountName` だけある場合（マネージド ID 接続）は、代わりに `StorageConnection__accountName` に同じ値を設定します。この場合、関数アプリの ID にストレージの「BLOB / キュー / テーブル データ共同作成者」ロールが必要です（管理者に確認）。

Azure にサービスを用意していない関数は、次の設定で無効にしておきます（値はすべて `true`）。

```text
AzureWebJobs.BlobTriggerEventGrid.Disabled        … Event Grid サブスクリプションの作成が必要（保留）
AzureWebJobs.ServiceBusSend.Disabled
AzureWebJobs.ServiceBusQueueTrigger.Disabled
AzureWebJobs.ServiceBusDeadLetterTrigger.Disabled
AzureWebJobs.ServiceBusTopicTrigger.Disabled
AzureWebJobs.EventHubSend.Disabled
AzureWebJobs.EventHubBatchTrigger.Disabled
AzureWebJobs.CosmosDbCreateItem.Disabled
AzureWebJobs.CosmosDbGetItem.Disabled
AzureWebJobs.CosmosDbChangeFeedTrigger.Disabled
AzureWebJobs.SqlInsertItem.Disabled
AzureWebJobs.SqlGetItems.Disabled
AzureWebJobs.SqlTriggerItems.Disabled
AzureWebJobs.KafkaSend.Disabled
AzureWebJobs.KafkaTrigger.Disabled
AzureWebJobs.RabbitMQSend.Disabled
AzureWebJobs.RabbitMQTrigger.Disabled
AzureWebJobs.SignalRNegotiate.Disabled
AzureWebJobs.SignalRBroadcast.Disabled
AzureWebJobs.SignalRMessagesTrigger.Disabled
AzureWebJobs.SignalRConnectionsTrigger.Disabled
```

Flex Consumption の場合は、ポーリング方式の Blob トリガーが使えないため `AzureWebJobs.BlobTrigger.Disabled` = `true` も追加します。

これで、追加のサービスなしで HTTP / Timer / Queue / Blob（出力・入力）/ Table / Durable / MCP / Event Grid（Webhook への直接 POST）を Azure 上で確認できます。
