# Azure 上での検証手順書（これ 1 本で進める）

管理者が作成した Flex Consumption の関数アプリに発行したプロジェクトを、**1 つずつ順番に**動作確認するための手順書です。
Azure CLI（az コマンド）は使いません。Azure ポータル、ブラウザー、VS2026 の `.http` ファイルだけで進めます。

- 上から順に進め、各表の「結果」欄に ○ / × と気づいたことを記入してください
- **× が出たらそこで止めて**、「困ったとき」の手順で情報を集めて Claude に報告してください（先に進むと原因が分かりにくくなります）

---

## 資料の整理（どれを見ればよいか）

| 資料 | 位置づけ |
|---|---|
| **この手順書** | **今後はこれだけ見て進める** |
| [next-steps.md](next-steps.md) | 発行〜関数一覧が表示されるまでの手順（完了済み。STEP 0 で × になったときだけ見直す） |
| [publish-checklist.md](publish-checklist.md) | 関数が表示されないときの原因調査用（完了済み。参考） |
| [application-insights-guide.md](application-insights-guide.md) | ログの見方（「困ったとき」で使う） |
| [azure-verification.md](azure-verification.md) | az スクリプトを使う手順（**権限の都合で保留中。使わない**） |
| [appsettings/flex-basic.json](appsettings/flex-basic.json) | 追加済みのアプリ設定（STEP 0 で確認） |
| [appsettings/isolate-http-only.json](appsettings/isolate-http-only.json) | 切り分け用（**使わない**。追加されていたら削除する） |

## これまでに解決したこと（おさらい）

| 症状 | 原因 | 対応（済み） |
|---|---|---|
| 関数一覧が空、`/api/hello` が 503 | Application Insights のパッケージのバージョンが混在し、ワーカー プロセスが起動直後に落ちていた | `Microsoft.ApplicationInsights.WorkerService` を 2.23.0 にして再発行 |
| `EventHub account connection string ... does not exist` | Azure に用意していないサービスの接続文字列の設定がない | ダミー値のアプリ設定を追加（`flex-basic.json`） |

## 今回の検証範囲

| 検証する（STEP 1〜9） | 検証しない（無効にしてある） |
|---|---|
| HTTP / Queue / Blob（出力・入力）/ Table / Timer / Durable / Event Grid（直接 POST）/ MCP | Service Bus / Event Hubs / Cosmos DB / SQL / Kafka / RabbitMQ / SignalR（Azure にサービスがない）、`BlobTrigger`（Flex では使えない）、`BlobTriggerEventGrid`（Event Grid サブスクリプションの作成が必要） |

---

## STEP 0. 今の状態を確認する

以降の手順の前提です。ここが全部 ○ でなければ [next-steps.md](next-steps.md) に戻ってください。

| # | やること | 期待値 | 結果 |
|---|---|---|---|
| 0-1 | ポータル → 関数アプリ →「概要」で状態を見る | 実行中（Running） | |
| 0-2 | ブラウザーで `https://<既定のドメイン>/api/hello?name=test` を開く（`<既定のドメイン>` は「概要」に表示されている） | `{"message":"Hello, test!"}`（503 なら 30 秒待って再読み込み） | |
| 0-3 | 「概要」の下部（または左メニュー「関数」）の一覧を見る | **60 個**の関数が表示され、22 個が「無効」 | |
| 0-4 | 「設定」→「環境変数」→「アプリ設定」タブで `StorageConnection` を検索 | 存在し、値が `AzureWebJobsStorage` と同じ（または `StorageConnection__accountName` がある） | |
| 0-5 | 同じ画面で `HttpPostJson.Disabled` を検索 | **見つからない**（見つかったら切り分け用の設定が残っている → `isolate-http-only.json` の 37 件を削除して「適用」→ 再起動） | |
| 0-6 | 同じ画面で `APPLICATIONINSIGHTS_CONNECTION_STRING` を検索 | 存在する（ないと STEP 4 以降のログ確認ができない） | |

---

## STEP 1. HTTP（ブラウザーだけで確認）

ブラウザーのアドレス バーに `https://<既定のドメイン>` に続けて入力します。

| # | URL | 期待値 | 結果 |
|---|---|---|---|
| 1-1 | `/api/hello` | `{"message":"Hello, world!"}`（`name` 省略時の既定値） | |
| 1-2 | `/api/items/42/books` | `{"id":42,"category":"books"}` | |
| 1-3 | `/api/items/42` | `{"id":42,"category":"(none)"}`（省略可能なルート パラメーター） | |
| 1-4 | `/api/items/abc` | 404（`id` は数値のみの制約） | |
| 1-5 | `/api/secure` | **401**（関数キーが必要。ローカルとの違い） | |
| 1-6 | `/api/throw` | 500（意図的な例外） | |

## STEP 2. `.http` ファイルの準備

POST / PUT のリクエストはブラウザーから送れないので、ここからは VS2026 の `http/FunctionsTest.http` を使います。

| # | やること | 期待値 | 結果 |
|---|---|---|---|
| 2-1 | ポータル → 関数アプリ →「関数」→「アプリ キー」を開く | 「ホスト キー」に `default`、「システム キー」に `eventgrid_extension` / `mcp_extension` がある | |
| 2-2 | リポジトリの `http` フォルダーに `http-client.env.json.user` を作り、下の内容を書く（値は 2-1 の「値の表示」でコピー） | 保存できる（Git の管理対象外なのでコミットされない） | |
| 2-3 | VS2026 で `http/FunctionsTest.http` を開き、右上の環境ドロップダウンで **azure** を選ぶ | `azure` が選べる | |
| 2-4 | 先頭の「GET + クエリ文字列」（`/hello?name=Functions`）の「要求の送信」をクリック | 200、`{"message":"Hello, Functions!"}` | |

```json
{
  "azure": {
    "host": "https://<既定のドメイン>",
    "functionKey": "<ホスト キー default の値>",
    "eventGridKey": "<システム キー eventgrid_extension の値>",
    "mcpKey": "<システム キー mcp_extension の値>"
  }
}
```

> `host` の末尾に `/` は付けません。

## STEP 3. HTTP（POST・認証）

`FunctionsTest.http` の「HTTP」の段落を上から送信します。

| # | リクエスト | 期待値 | 結果 |
|---|---|---|---|
| 3-1 | POST JSON（正常） | 200、送った JSON がそのまま返る | |
| 3-2 | POST JSON（text 欠落） | 400、`{"error":"text は必須です"}` | |
| 3-3 | POST JSON（不正な JSON） | 400、`{"error":"JSON の形式が不正です"}` | |
| 3-4 | 認証レベル Function（`/secure?code=...`） | 200、`{"message":"関数キーで認証されました"}`（STEP 1-5 との違いを確認） | |

## STEP 4. ログが見られることを確認する

STEP 5 以降は「HTTP の応答」ではなく「ログ」で動作を確認する関数が出てくるので、先にログの見方を確認します。
詳しい画面操作は [application-insights-guide.md](application-insights-guide.md) の手順 3〜4 を参照してください。

| # | やること | 期待値 | 結果 |
|---|---|---|---|
| 4-1 | 関数アプリ →「監視」→「Application Insights」→「Application Insights データの表示」→「ログ」（KQL モード）を開く | クエリの入力欄が表示される | |
| 4-2 | 下の「関数のログを見るクエリ」の `<関数名>` を `HttpHello` にして実行 | STEP 1〜3 で呼んだ時刻の `HttpHello: name=...` が出る（届くまで 2〜5 分かかる） | |
| 4-3 | 関数アプリ →「監視」→「ログ ストリーム」を開いたまま、別タブで `/api/hello?name=stream` を開く | ログ ストリームに `HttpHello: name=stream` が流れる（すぐ確認したいときはこちらが便利） | |

**関数のログを見るクエリ**（以降のステップでも `<関数名>` を変えて使います）

```kusto
traces
| where timestamp > ago(30m)
| where message has "<関数名>"
| project timestamp, severityLevel, message
| order by timestamp desc
| take 50
```

## STEP 5. Queue（HTTP → Queue → Blob の連鎖、poison キュー）

| # | やること | 期待値 | 結果 |
|---|---|---|---|
| 5-1 | `.http` の「HTTP → Queue ... の連鎖」（`POST /enqueue`）を送信 | 202、`id` と `text: "chain test"` を含む JSON | |
| 5-2 | 4 のクエリを `QueueTrigger:` で実行（またはログ ストリームを見る） | `QueueTrigger: id=<5-1 の id>, text=chain test, dequeueCount=1` | |
| 5-3 | ポータル → 関数アプリが使っているストレージ アカウント →「ストレージ ブラウザー」→「BLOB コンテナー」→ `results` | `<GUID>.json` ができていて、中身の `text` が `[processed] chain test` | |
| 5-4 | 同じストレージ ブラウザー →「キュー」→ `sample-queue-sdk`（なければ「+ キュー」で作成）→「メッセージの追加」で本文 `hello` を追加（**「メッセージ本文を Base64 でエンコードする」にチェック**） | 4 のクエリを `QueueTriggerSdkType` で実行すると `body=hello` が 1 回出る | |
| 5-5 | 5-4 と同じ方法で本文 `please fail` を追加 | `QueueTriggerSdkType` が `dequeueCount=1〜3` で 3 回失敗し、その後 `QueuePoisonTrigger: poison に移ったメッセージ body=please fail` が出る（1 分ほどかかる） | |

> ストレージ アカウント名は、関数アプリの「環境変数」→ `AzureWebJobsStorage` の `AccountName=` の後ろ（または `AzureWebJobsStorage__accountName` の値）で分かります。
> ストレージ ブラウザーを開く権限がない場合は、5-3〜5-5 は「権限なし」と記入して先に進んでください。

## STEP 6. Blob（出力・入力・一覧）

| # | リクエスト（`.http` の「Storage: Blob」） | 期待値 | 結果 |
|---|---|---|---|
| 6-1 | Blob 出力（`PUT /blobs/hello.txt`） | 200、`{"saved":"uploads/hello.txt","length":11}` | |
| 6-2 | Blob 入力（`GET /blobs/hello.txt`）※ブラウザーでも可 | 200、`content` が `Hello Blob!` | |
| 6-3 | Blob 入力（存在しない → 404） | 404、`not-found.txt は存在しません` | |
| 6-4 | Blob 一覧（`GET /blobs`）※ブラウザーでも可 | `["hello.txt"]`（6-1 で保存したもの） | |

> `BlobTrigger` は Flex Consumption では使えないため無効にしてあります。6-1 の後にトリガーのログが出ないのは正常です。

## STEP 7. Table

| # | リクエスト（`.http` の「Storage: Table」） | 期待値 | 結果 |
|---|---|---|---|
| 7-1 | Table 出力（`POST /tables/pk1`） | 200、`partitionKey: "pk1"`・`rowKey`（32 文字）・`text: "table row"` | |
| 7-2 | Table 一覧（`GET /tables/pk1`）※ブラウザーでも可 | 7-1 の行を含む配列 | |
| 7-3 | Table 1 件取得：`REPLACE_ROW_KEY` を 7-1 の `rowKey` に書き換えて送信 | 200、7-1 と同じ行（`timestamp` 付き） | |
| 7-4 | 7-3 の `rowKey` を 1 文字変えて送信 | 404 | |

## STEP 8. Timer

アプリ設定 `TimerSchedule` = `0 */5 * * * *` なので、`TimerFromSetting` は **5 分ごと**（毎時 0, 5, 10 … 分）に動きます。`TimerHourly` は毎時 0 分 30 秒です。

| # | やること | 期待値 | 結果 |
|---|---|---|---|
| 8-1 | ほかのステップを進めながら 10 分以上待ち、4 のクエリを `TimerFromSetting` で実行 | 5 分おきに `TimerFromSetting: 実行 ..., 次回 ..., 遅延=False` が出る | |
| 8-2 | 1 時間以上たってから 4 のクエリを `TimerHourly` で実行（`ago(30m)` を `ago(2h)` に変える） | 毎時 0 分 30 秒ごろに `TimerHourly: 実行 ...` が出る | |
| 8-3 | 8-1 の結果で、同じ時刻の行が 2 つ以上ないか見る | 1 回の時刻につき **1 行だけ**（インスタンスが増えても重複しない） | |

> Flex Consumption ではリクエストがないとインスタンスが 0 台になりますが、Timer はプラットフォームが起こしてくれるので、待つだけで動くのが正常です。

## STEP 9. Durable Functions

`.http` の「Durable Functions」の段落を使います。起動すると 202 と `statusQueryGetUri` が返るので、**その URL をブラウザーで開いて**（数秒おきに再読み込みして）`runtimeStatus` を確認します。

| # | リクエスト | 期待値（`statusQueryGetUri` の内容） | 結果 |
|---|---|---|---|
| 9-1 | チェーン（`ChainingOrchestrator`） | `Completed`、`output` が `["Hello Tokyo!","Hello Osaka!","Hello Nagoya!"]` | |
| 9-2 | ファンアウト / ファンイン（`FanOutFanInOrchestrator`） | `Completed`、`output` が `385`（1〜10 の二乗の合計）。4 のクエリを `Square:` で実行すると「一時的な失敗」の警告が出ていることがある（リトライされて最後は成功） | |
| 9-3 | モニター（`MonitorOrchestrator`） | 約 15 秒後に `Completed`、`output` が `完了 (attempt=4)` | |
| 9-4 | サブオーケストレーション（`ParentOrchestrator`） | `Completed`、`output` に `chain`（9-1 と同じ）と `sum: 385` | |
| 9-5 | 承認待ち（`ApprovalOrchestrator`）を起動 → 応答の `id` で「承認イベント送信」の `REPLACE_INSTANCE_ID` を書き換え、**30 秒以内に**送信 | `Completed`、`output` が `Approved` | |
| 9-6 | 承認待ちをもう一度起動し、何もせず 30 秒以上待つ | `Completed`、`output` が `TimedOut` | |
| 9-7 | エンティティ: リセット → 加算（`value=5`）を 2 回 → 状態取得（`GET /durable/counter/c1`） | `{"key":"c1","value":10}`（反映まで数秒かかるので、0 なら少し待って再送信） | |

> 9-2 で `runtimeStatus` が `Failed` になった場合は、`Square` が 3 回連続で失敗した可能性があります（約 3% の確率）。もう一度起動して `Completed` になれば正常です。

## STEP 10. Event Grid（Webhook へ直接 POST）

Azure に Event Grid のリソースはないので、イベントの代わりに関数の Webhook へ直接 POST します。

| # | リクエスト（`.http` の「Event Grid」） | 期待値 | 結果 |
|---|---|---|---|
| 10-1 | CloudEvents スキーマ | 202 | |
| 10-2 | Event Grid スキーマ | 202 | |
| 10-3 | 4 のクエリを `EventGrid` で実行 | `EventGridCloudEventTrigger: type=Sample.Created, ...` と `EventGridSchemaTrigger: eventType=Sample.Created, ...` が出る | |
| 10-4 | 10-1 の URL から `&code={{eventGridKey}}` を消して送信 | 401（システム キーが必要） | |

## STEP 11. MCP

| # | リクエスト（`.http` の「MCP」） | 期待値 | 結果 |
|---|---|---|---|
| 11-1 | initialize | 200、応答に `"name":"FunctionsTest"` を含む | |
| 11-2 | ツール一覧 | `get_time` と `add` が返る（セッション ID を求めるエラーになった場合は、11-1 の応答ヘッダー `Mcp-Session-Id` をリクエストに `Mcp-Session-Id: <値>` として追記して再送信） | |
| 11-3 | ツール呼び出し（`add`、1.5 + 2） | 結果に `3.5` を含む | |
| 11-4 | 11-1 から `x-functions-key` の行を消して送信 | 401 | |

## STEP 12. 例外が記録されているか（まとめの確認）

| # | やること | 期待値 | 結果 |
|---|---|---|---|
| 12-1 | Application Insights →「調査」→「失敗」→「例外」タブ | STEP 1-6 の `InvalidOperationException: 意図的な例外です`、STEP 5-5 の `意図的な失敗（poison キュー確認用）` が記録されている | |
| 12-2 | 下のクエリで関数ごとの実行回数を見る | ここまでに呼んだ関数が並び、失敗は `HttpThrow`・`QueueTriggerSdkType` など意図したものだけ | |

```kusto
requests
| where timestamp > ago(3h)
| summarize 実行回数 = count(), 失敗 = countif(success == false) by operation_Name
| order by operation_Name asc
```

**STEP 12 まで ○ なら、Azure 上で確認できる範囲の検証は完了です。**

---

## 困ったとき

× が出たら、次の情報をそろえて Claude に報告してください（手書きの要約でかまいません。キーや接続文字列は伏せてください）。

1. どの STEP の何番が × だったか
2. 返ってきたステータス コードと本文（`.http` の応答画面、またはブラウザーの表示）
3. 4 のクエリを、その関数名で実行した結果
4. 次の「エラーだけを見るクエリ」の結果の**最初の数件**

```kusto
union traces, exceptions
| where timestamp > ago(30m)
| where severityLevel >= 3 or itemType == "exception"
| where coalesce(innermostMessage, message) !contains "disposed"
| project timestamp, itemType, message, outerMessage, innermostMessage
| order by timestamp asc
| take 30
```

よくある原因:

| 症状 | 考えられる原因 |
|---|---|
| どの URL も 503 | コールド スタート中（30 秒待つ）か、ワーカーが落ちている（STEP 0 からやり直し、ダメなら [next-steps.md](next-steps.md) の 5 章） |
| Queue / Blob / Table だけエラー | `StorageConnection` が間違っている（STEP 0-4） |
| `.http` で「変数が見つからない」 | `http-client.env.json.user` の場所・名前の誤りか、環境が azure になっていない（STEP 2） |
| 401 になるはずでないところで 401 | `functionKey` / `eventGridKey` / `mcpKey` の値の貼り間違い（STEP 2-1） |
| ログが 0 件 | 届くまで 2〜5 分待つ。時間範囲（`ago(30m)`）を広げる |
