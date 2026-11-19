# Azure 上での検証手順書（これ 1 本で進める）

管理者が作成した Flex Consumption の関数アプリに発行したプロジェクトを、**1 つずつ順番に**動作確認するための手順書です。
Azure CLI（az コマンド）は使いません。Azure ポータル、ブラウザー、VS2026 の `.http` ファイルだけで進めます。

- 上から順に進め、各表の「結果」欄に ○ / × と気づいたことを記入してください
- **× が出たらそこで止めて**、「困ったとき」の手順で情報を集めて Claude に報告してください（先に進むと原因が分かりにくくなります）
- 何度も出てくる操作（`.http` の送り方、ログの見方、ストレージの中身の見方）は、先に「**共通の操作**」の章で説明しています

---

## 資料の整理（どれを見ればよいか）

| 資料 | 位置づけ |
|---|---|
| **この手順書** | **今後はこれだけ見て進める** |
| [next-steps.md](next-steps.md) | 発行〜関数一覧が表示されるまでの手順（完了済み。STEP 0 で × になったときだけ見直す） |
| [publish-checklist.md](publish-checklist.md) | 関数が表示されないときの原因調査用（完了済み。参考） |
| [application-insights-guide.md](application-insights-guide.md) | ログの見方の補足（この手順書の「共通の操作 B」で足りなければ見る） |
| [azure-verification.md](azure-verification.md) | az スクリプトを使う手順（**権限の都合で保留中。使わない**） |
| [appsettings/flex-basic.json](appsettings/flex-basic.json) | 追加済みのアプリ設定（STEP 0 で確認） |
| [appsettings/isolate-http-only.json](appsettings/isolate-http-only.json) | 切り分け用（**使わない**。追加されていたら削除する） |

## これまでの経過

| 症状 | 原因 | 対応 |
|---|---|---|
| 関数一覧が空、`/api/hello` が 503 | Application Insights のパッケージのバージョンが混在し、ワーカー プロセスが起動直後に落ちていた | `Microsoft.ApplicationInsights.WorkerService` を 2.23.0 にして再発行（済み） |
| `EventHub account connection string ... does not exist` | Azure に用意していないサービスの接続文字列の設定がない | ダミー値のアプリ設定を追加（済み） |
| STEP 7-3 / 7-4（Table の 1 件取得）が 500 | `TableInput` で自作の型（`SampleEntity`）に直接読み込む書き方が Azure 上で失敗した | `TableClient` で読み込む書き方に変更。**再発行が必要**（STEP 7 の前に「再発行」の手順あり） |

## 今回の検証範囲

| 検証する | 検証しない（無効にしてある） |
|---|---|
| HTTP / Queue / Blob（出力・入力）/ Table / Timer / Durable / Event Grid（直接 POST）/ MCP | Service Bus / Event Hubs / Cosmos DB / SQL / Kafka / RabbitMQ / SignalR（Azure にサービスがない）、`BlobTrigger`（Flex では使えない）、`BlobTriggerEventGrid`（Event Grid サブスクリプションの作成が必要） |

---

## 共通の操作

### A. `.http` ファイルでリクエストを送る

1. VS2026 で `http/FunctionsTest.http` を開く（ソリューション エクスプローラーの「http」フォルダーの中）
2. エディターの右上にある環境のドロップダウンで **azure** が選ばれていることを確認する
3. 送りたいリクエストを探す。各リクエストは `### 説明` の行で始まるので、**Ctrl + F** でその説明の文字（例: `Table 出力`）を検索すると早い
4. `GET` / `POST` / `PUT` で始まる行の**すぐ上**に、小さく「**要求の送信**」と表示されているのでクリックする
5. 画面の右側（または下側）に応答の画面が開く。見るところは 2 つ
   - **ステータス コード**: 応答画面の上部に `200 OK` や `404 Not Found` のように出る数字
   - **本文**: その下に出る JSON（`{ ... }` の部分）

> 応答の本文から値をコピーするときは、応答画面の文字をマウスで選んで Ctrl + C でコピーできます。

### B. ログ（Application Insights）を見る

関数の中で書き出したログ（例: `QueueTrigger: id=...`）は Application Insights に届きます。**届くまで 2〜5 分かかる**ので、操作してから少し待って見てください。

**最初の 1 回だけ（画面を開く）**

1. Azure ポータルで関数アプリを開く
2. 左メニューの「監視」→「**Application Insights**」をクリック
3. 画面上部の「**Application Insights データの表示**」（リンク）をクリック → Application Insights の画面に移る
4. 左メニューの「監視」→「**ログ**」をクリック
5. 「クエリ」のサンプル一覧のウィンドウが出たら、右上の **×** で閉じる
6. 画面右上の切り替えが「シンプル モード」になっていたら「**KQL モード**」に切り替える

> この画面はブラウザーのお気に入りに登録しておくと、次回から 1 クリックで開けます。

**クエリを実行する（毎回）**

1. 大きな入力欄（エディター）の中身を **Ctrl + A → Delete** で全部消す
2. この手順書に書いてあるクエリ（灰色の枠の中）を**そのまま全部**コピーして貼り付ける
3. 入力欄の上にある「**実行**」ボタン（▷）をクリック
4. 下に表が出る。`message` 列に関数のログが出ていれば OK
   - 「結果が見つかりませんでした」と出たら、2〜3 分待ってからもう一度「実行」を押す
   - 「時間範囲」は「**クエリで設定**」のままにする（クエリの中の `ago(30m)` = 直近 30 分 が使われる）

### C. ストレージの中身（キュー・Blob）を見る

**ストレージ アカウントの見つけ方**

1. 関数アプリの左メニュー「設定」→「**環境変数**」→「アプリ設定」タブ
2. `AzureWebJobsStorage` の行の「値」列にある「値の表示」をクリック
3. `AccountName=xxxxx;` の `xxxxx` がストレージ アカウント名（`AzureWebJobsStorage__accountName` しかない場合は、その値がアカウント名）
4. ポータル上部の検索ボックスにそのアカウント名を入れて開く

**中身を見る**

1. ストレージ アカウントの左メニュー「**ストレージ ブラウザー**」をクリック
2. 「**BLOB コンテナー**」を開くと Blob の入れ物（`results`、`uploads` など）、「**キュー**」を開くとキューの一覧が出る
3. 名前をクリックすると中身の一覧が出る。Blob はファイル名をクリック →「編集」で中身を見られる

> ストレージ ブラウザーを開く権限がない場合は、その手順の「結果」欄に「権限なし」と書いて先に進んでください。

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
| 0-6 | 同じ画面で `APPLICATIONINSIGHTS_CONNECTION_STRING` を検索 | 存在する（ないとログの確認ができない） | |

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
| 2-4 | 「共通の操作 A」の手順で、先頭の「GET + クエリ文字列」を送信 | 200、`{"message":"Hello, Functions!"}` | |

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

`.http` の「HTTP」の段落を上から送信します。

| # | リクエスト | 期待値 | 結果 |
|---|---|---|---|
| 3-1 | POST JSON（正常） | 200、送った JSON がそのまま返る | |
| 3-2 | POST JSON（text 欠落） | 400、`{"error":"text は必須です"}` | |
| 3-3 | POST JSON（不正な JSON） | 400、`{"error":"JSON の形式が不正です"}` | |
| 3-4 | 認証レベル Function（`/secure?code=...`） | 200、`{"message":"関数キーで認証されました"}`（STEP 1-5 との違いを確認） | |

## STEP 4. ログが見られることを確認する

「共通の操作 B」の手順で Application Insights のログ画面を開き、次のクエリを実行します。

```kusto
traces
| where timestamp > ago(30m)
| where message has "HttpHello"
| project timestamp, message
| order by timestamp desc
```

| # | やること | 期待値 | 結果 |
|---|---|---|---|
| 4-1 | 上のクエリを実行 | `HttpHello: name=test` や `HttpHello: name=Functions` の行が出る（STEP 1〜3 で呼んだもの） | |

> 0 件の場合は、ブラウザーで `/api/hello?name=log` を開き、3 分待ってからもう一度「実行」を押してください。それでも 0 件なら STEP 0-6 を確認します。

---

## STEP 5. Queue（キュー）

**何を確かめるか**: キューは「あとで処理してほしい仕事」を入れておく箱です。箱にメッセージを入れると、関数が自動で取り出して処理します。
HTTP の応答では結果が分からないので、**ログ**と**ストレージの中身**で確認します。

### 5-A. HTTP → キュー → Blob の連鎖

流れ: ① `.http` で `/api/enqueue` を呼ぶ → ② `HttpToQueue` 関数が `sample-queue` キューにメッセージを入れる → ③ `QueueTrigger` 関数が自動で取り出し、`results` コンテナーにファイル（Blob）を保存する

| # | やること | 期待値 | 結果 |
|---|---|---|---|
| 5-1 | `.http` で「**HTTP → Queue**」を検索し、そのリクエストを送信 | ステータス **202**、本文に `"id": "..."` と `"text": "chain test"` が出る。**`id` の値をメモ**する | |
| 5-2 | 3 分待ってから、下の「5-2 のクエリ」を実行 | `QueueTrigger: id=<5-1 の id>, text=chain test, dequeueCount=1` という行が出る（= キューから取り出されて処理された） | |
| 5-3 | 「共通の操作 C」でストレージ ブラウザー →「BLOB コンテナー」→ `results` を開く | ファイル名が英数字の `xxxxxxxx-....json` のファイルがある。開くと中に `"text":"[processed] chain test"` が書いてある（= 処理結果が保存された） | |

5-2 のクエリ:

```kusto
traces
| where timestamp > ago(30m)
| where message has "QueueTrigger:"
| project timestamp, message
| order by timestamp desc
```

### 5-B. 失敗したメッセージが poison キューに移る

流れ: 本文に `fail` を含むメッセージを `sample-queue-sdk` キューに入れる → `QueueTriggerSdkType` 関数がわざと失敗する → 3 回失敗すると、メッセージが自動で `sample-queue-sdk-poison`（失敗したメッセージ置き場）に移る → `QueuePoisonTrigger` 関数がそれを記録する

| # | やること | 期待値 | 結果 |
|---|---|---|---|
| 5-4 | ストレージ ブラウザー →「キュー」を開く。`sample-queue-sdk` がなければ、上部の「**+ キュー**」をクリックし、名前に `sample-queue-sdk` と入れて「OK」 | 一覧に `sample-queue-sdk` がある | |
| 5-5 | `sample-queue-sdk` をクリック → 上部の「**+ メッセージの追加**」→「メッセージ テキスト」に `hello` と入力 →「**メッセージ本文を Base64 でエンコードする**」に**チェックを入れる** →「OK」 | 数秒〜1 分で一覧から消える（= 関数が取り出した） | |
| 5-6 | もう一度「+ メッセージの追加」で、テキストを `please fail` にして同じように追加（Base64 にチェック） | 1〜2 分後、キューの一覧に `sample-queue-sdk-poison` ができる（左上の「最新の情報に更新」を押す） | |
| 5-7 | 3 分待ってから、下の「5-7 のクエリ」を実行 | 次の 3 種類の行が出る<br>・`body=hello` が 1 行（5-5 の成功）<br>・`body=please fail` が `dequeueCount=1`・`2`・`3` の 3 行（3 回失敗）<br>・`QueuePoisonTrigger: poison に移ったメッセージ body=please fail` が 1 行 | |

5-7 のクエリ:

```kusto
traces
| where timestamp > ago(30m)
| where message has "QueueTriggerSdkType:" or message has "QueuePoisonTrigger:"
| project timestamp, message
| order by timestamp asc
```

> 5-5 で Base64 にチェックを入れ忘れると、関数がメッセージを読めずに失敗し、`hello` も poison キューに移ります。その場合はチェックを入れてやり直してください。

## STEP 6. Blob（出力・入力・一覧）

| # | リクエスト（`.http` の「Storage: Blob」） | 期待値 | 結果 |
|---|---|---|---|
| 6-1 | Blob 出力（`PUT /blobs/hello.txt`） | 200、`{"saved":"uploads/hello.txt","length":11}` | |
| 6-2 | Blob 入力（`GET /blobs/hello.txt`）※ブラウザーでも可 | 200、`content` が `Hello Blob!` | |
| 6-3 | Blob 入力（存在しない → 404） | 404、`not-found.txt は存在しません` | |
| 6-4 | Blob 一覧（`GET /blobs`）※ブラウザーでも可 | `["hello.txt"]`（6-1 で保存したもの） | |

> `BlobTrigger` は Flex Consumption では使えないため無効にしてあります。6-1 の後にトリガーのログが出ないのは正常です。

## STEP 7. Table

### 7-0. 再発行（7-3 / 7-4 の 500 エラーの修正を反映する）

| # | やること | 期待値 | 結果 |
|---|---|---|---|
| 7-0-1 | VS2026 の「Git」→「プル」 | エラーなく完了する | |
| 7-0-2 | `src/FunctionsTest/Functions/Storage/TableFunctions.cs` を開き、`TableGetOne` を探す | 引数に `TableClient table` があり、`GetEntityIfExistsAsync` を使っている | |
| 7-0-3 | ソリューション エクスプローラーで `FunctionsTest` を右クリック →「発行」→「発行」 | 「出力」ウィンドウの最後に「発行が成功しました」 | |
| 7-0-4 | 1〜2 分待って、ブラウザーで `/api/hello` を開く | `{"message":"Hello, world!"}`（503 なら 30 秒待って再読み込み） | |

### 7-1〜7-4. 確認

| # | リクエスト（`.http` の「Storage: Table」） | 期待値 | 結果 |
|---|---|---|---|
| 7-1 | Table 出力（`POST /tables/pk1`） | 200、`partitionKey: "pk1"`・`rowKey`（32 文字の英数字）・`text: "table row"`。**`rowKey` の値をコピー**しておく | |
| 7-2 | Table 一覧（`GET /tables/pk1`）※ブラウザーでも可 | 7-1 の行を含む配列 | |
| 7-3 | 「Table 1 件取得」の行 `GET {{api}}/tables/pk1/REPLACE_ROW_KEY` の **`REPLACE_ROW_KEY` の部分**を、7-1 でコピーした `rowKey` に書き換えて送信 | 200、7-1 と同じ行（`timestamp` 付き） | |
| 7-4 | 7-3 の `rowKey` の最後の 1 文字を別の文字に変えて送信 | 404、`... は存在しません` | |

> 送信が終わったら、7-3 で書き換えた行は Ctrl + Z で元に戻しておいてください（キーではないのでコミットしても問題はありません）。

## STEP 8. Timer（決まった時刻に自動で動く関数）

**何を確かめるか**: Timer の関数は、だれも呼ばなくても決まった時刻に自動で動きます。**待つだけ**で、結果はログで確認します。

- `TimerFromSetting`: **5 分ごと**（毎時 0 分、5 分、10 分 …）に動く
- `TimerHourly`: **1 時間ごと**（毎時 0 分 30 秒）に動く

STEP 9 以降を進めて**時間がたってから**確認すると効率的です。

| # | やること | 期待値 | 結果 |
|---|---|---|---|
| 8-1 | 前回の確認から 15 分以上たってから、下の「8-1 のクエリ」を実行 | `TimerFromSetting: 実行 ...` の行が **5 分おき**に並ぶ（例: 10:00、10:05、10:10）。各行の最後が `遅延=False` | |
| 8-2 | 8-1 の結果の `timestamp` 列を見る | 同じ時刻（同じ「分」）の行が **2 つ以上ない**（関数アプリが複数台に増えても、1 回分しか動かない） | |
| 8-3 | 検証を始めてから 1 時間以上たってから、下の「8-3 のクエリ」を実行 | `TimerHourly: 実行 ...` の行が、毎時 0 分ごろに 1 行ずつ出る | |

8-1 のクエリ（直近 1 時間）:

```kusto
traces
| where timestamp > ago(1h)
| where message has "TimerFromSetting:"
| project timestamp, message
| order by timestamp asc
```

8-3 のクエリ（直近 3 時間）:

```kusto
traces
| where timestamp > ago(3h)
| where message has "TimerHourly:"
| project timestamp, message
| order by timestamp asc
```

> `timestamp` は UTC（日本時間 − 9 時間）で表示されます。分の部分だけ見れば十分です。

## STEP 9. Durable Functions（長く続く処理）

**何を確かめるか**: Durable Functions は「複数の処理を順番や並列に実行し、途中で待つこともできる」仕組みです。
起動するとすぐに「受け付けました」（202）が返り、処理は裏で進みます。**進み具合を見るための URL**（`statusQueryGetUri`）が返ってくるので、それをブラウザーで開いて結果を確認します。

### 進み具合の見方（9-1〜9-6 で共通）

1. `.http` でオーケストレーションを起動する（例: 「**チェーン**」を検索して送信）
2. 応答のステータスが **202** で、本文が次のような JSON になっていることを確認する

   ```json
   {
     "id": "abc123...",
     "purgeHistoryDeleteUri": "https://...",
     "sendEventPostUri": "https://...",
     "statusQueryGetUri": "https://<既定のドメイン>/runtime/webhooks/durabletask/instances/abc123...?code=...",
     "terminatePostUri": "https://..."
   }
   ```

3. `"statusQueryGetUri":` の後ろの `https://` から始まる URL を、**前後の `"` を含めずに**コピーする
4. ブラウザーの新しいタブのアドレス バーに貼り付けて開く
5. 表示された JSON の **`runtimeStatus`** を見る
   - `Pending` / `Running` … まだ処理中。数秒待って **F5**（再読み込み）
   - `Completed` … 完了。**`output`** に結果が入っている
   - `Failed` … 失敗

| # | `.http` で検索して送信するもの | 期待値（ブラウザーで見る `runtimeStatus` と `output`） | 結果 |
|---|---|---|---|
| 9-1 | チェーン | `Completed`、`output` が `["Hello Tokyo!","Hello Osaka!","Hello Nagoya!"]`（3 つの処理が順番に実行された） | |
| 9-2 | ファンアウト / ファンイン | `Completed`、`output` が `385`（10 個の処理を並列に実行し、結果を合計した） | |
| 9-3 | モニター | 約 15〜20 秒後に `Completed`、`output` が `完了 (attempt=4)`。途中で F5 を押すと `customStatus` の `attempt` が 1→2→3→4 と増えていく | |
| 9-4 | サブオーケストレーション | `Completed`、`output` が `{"chain":["Hello Tokyo!", ...],"sum":385}`（9-1 と 9-2 をまとめて実行した） | |

> 9-2 で `Failed` になった場合は、わざと入れている「ときどき失敗する処理」が 3 回連続で失敗した可能性があります（約 3% の確率）。もう一度起動して `Completed` になれば正常です。

### 9-5〜9-6. 承認待ち（外部からの合図を待つ）

`ApprovalOrchestrator` は、起動すると「承認」の合図を **30 秒**待ちます。30 秒以内に合図を送れば `Approved`、送らなければ `TimedOut` になります。
30 秒は短いので、**先に手順をすべて読んでから**始めてください。

| # | やること | 期待値 | 結果 |
|---|---|---|---|
| 9-5-1 | `.http` で「**承認待ち**」を検索して送信 | 202。本文の `"id":` の値（`"` の中の英数字）をコピーする | |
| 9-5-2 | すぐに「**承認イベント送信**」を検索し、`POST {{api}}/durable/approve/REPLACE_INSTANCE_ID?approved=true` の **`REPLACE_INSTANCE_ID` の部分**を、コピーした `id` に書き換えて送信 | 202、`"approved": true` | |
| 9-5-3 | 9-5-1 の応答の `statusQueryGetUri` をブラウザーで開く | `Completed`、`output` が `Approved`、`customStatus` が `承認` | |
| 9-6-1 | もう一度「承認待ち」を送信し、今度は**何もせず** 40 秒待つ | — | |
| 9-6-2 | 9-6-1 の応答の `statusQueryGetUri` をブラウザーで開く | `Completed`、`output` が `TimedOut`、`customStatus` が `タイムアウト` | |

> 9-5 で間に合わず `TimedOut` になった場合は、9-6 の確認ができたことにして、9-5 を最初からやり直してください。
> 終わったら、書き換えた `REPLACE_INSTANCE_ID` の行は Ctrl + Z で元に戻しておいてください。

### 9-7. エンティティ（数を覚えておくカウンター）

`c1` という名前のカウンターに足し算をして、値が覚えられているかを確認します。

| # | `.http` で検索して送信するもの | 期待値 | 結果 |
|---|---|---|---|
| 9-7-1 | 「エンティティ: リセット」 | 202 | |
| 9-7-2 | 「エンティティ: 加算」を **2 回**送信 | どちらも 202、`"value": 5` | |
| 9-7-3 | 5 秒待ってから「エンティティ: 状態取得」を送信（ブラウザーで `/api/durable/counter/c1` を開いてもよい） | `{"key":"c1","value":10}`（0 + 5 + 5）。`10` より小さいときは数秒待って再送信 | |

## STEP 10. Event Grid（イベントの通知を受け取る関数）

**何を確かめるか**: Event Grid は「何かが起きた（例: ファイルが作られた）」という通知を関数に届ける Azure のサービスです。
今回は Event Grid 本体を用意していないので、**Event Grid のふりをして**、`.http` から関数の受付窓口（Webhook）に通知を直接送ります。
受付窓口には `eventGridKey`（STEP 2 で設定したシステム キー）が必要です。

| # | やること | 期待値 | 結果 |
|---|---|---|---|
| 10-1 | `.http` で「**CloudEvents スキーマ**」を検索し、その下の `POST {{host}}/runtime/webhooks/EventGrid?...` の上の「要求の送信」をクリック | ステータス **202**（または 200）。本文は空でよい | |
| 10-2 | 「**Event Grid スキーマ（配列で送る）**」を検索して同じように送信 | ステータス **202**（または 200） | |
| 10-3 | 3 分待ってから、下の「10-3 のクエリ」を実行 | 次の 2 行が出る<br>・`EventGridCloudEventTrigger: type=Sample.Created, source=/local/test, subject=items/1, ...`<br>・`EventGridSchemaTrigger: eventType=Sample.Created, subject=items/2, ...` | |
| 10-4 | 10-1 のリクエストの行の末尾にある `&code={{eventGridKey}}` を消して送信 | ステータス **401**（キーがないと受け付けない） | |
| 10-5 | 10-4 で消した部分を **Ctrl + Z** で元に戻す | 行の末尾が `&code={{eventGridKey}}` に戻っている | |

10-3 のクエリ:

```kusto
traces
| where timestamp > ago(30m)
| where message has "EventGridCloudEventTrigger:" or message has "EventGridSchemaTrigger:"
| project timestamp, message
| order by timestamp desc
```

> 10-1 / 10-2 が 401 になる場合は、STEP 2 の `eventGridKey` の値（システム キー `eventgrid_extension`）が間違っています。404 になる場合は、Event Grid の関数が無効になっていないか（`AzureWebJobs.EventGrid...Disabled`）をアプリ設定で確認してください。

## STEP 11. MCP（AI エージェントから呼べる関数）

**何を確かめるか**: MCP は、GitHub Copilot などの AI エージェントが「ツール」として関数を呼び出すための仕組みです。
本来は AI エージェントから接続しますが、今回は `.http` から AI エージェントと同じ内容のリクエストを送って確認します。
送るリクエストは 3 つで、**上から順番に**送ります（① 接続のあいさつ → ② 使えるツールの一覧 → ③ ツールの実行）。

| # | `.http` で検索して送信するもの | 期待値 | 結果 |
|---|---|---|---|
| 11-1 | 「**### initialize**」 | ステータス **200**。本文のどこかに `"name":"FunctionsTest"` が含まれる | |
| 11-2 | 「**### ツール一覧**」 | ステータス **200**。本文に `"get_time"` と `"add"` が含まれる | |
| 11-3 | 「**### ツール呼び出し**」 | ステータス **200**。本文に `3.5` が含まれる（`add` ツールで 1.5 + 2 を計算した結果） | |
| 11-4 | 11-1 のリクエストの `x-functions-key: {{mcpKey}}` の行を一時的に消して送信 → 終わったら **Ctrl + Z** で戻す | ステータス **401**（キーがないと受け付けない） | |

**本文の見え方について**: 応答の本文は次のように `event:` と `data:` で始まる形になることがあります。`data:` の後ろの JSON に期待値の文字が含まれていれば OK です。

```text
event: message
data: {"result":{"protocolVersion":"2025-06-18","serverInfo":{"name":"FunctionsTest","version":"1.0.0"}, ...},"id":1,"jsonrpc":"2.0"}
```

**11-2 / 11-3 がエラーになった場合**（本文に `session` や `Mcp-Session-Id` という文字がある、またはステータス 400）:

1. 11-1 をもう一度送信し、応答画面の「**ヘッダー**」（Headers）タブを開く
2. `Mcp-Session-Id` という名前の値をコピーする
3. 11-2 のリクエストの `x-functions-key: {{mcpKey}}` の行の**下**に、新しい行として `Mcp-Session-Id: <コピーした値>` を追加して送信する（11-3 も同様）
4. それでもエラーになる場合は、本文を Claude に報告してください

## STEP 12. まとめの確認（エラーの記録）

**何を確かめるか**: ここまでの手順で、**わざと**エラーを起こした関数がいくつかあります（STEP 1-6 の `/api/throw`、STEP 5-6 の `please fail`）。
それらのエラーが Application Insights に正しく記録されているか、**意図しないエラーが起きていないか**を確認します。

| # | やること | 期待値 | 結果 |
|---|---|---|---|
| 12-1 | 「共通の操作 B」でログ画面を開き、下の「12-1 のクエリ」を実行 | 関数名ごとに 1 行ずつ表が出る（次の表を参照） | |
| 12-2 | 12-1 の表の `失敗` 列を見る | 0 より大きいのが **`HttpThrow`** と **`QueueTriggerSdkType`**（と 9-2 で失敗した場合の `Square`）**だけ** | |
| 12-3 | 下の「12-3 のクエリ」を実行 | `意図的な例外です`（`/api/throw`）と `意図的な失敗（poison キュー確認用）`（`please fail`）が出る。**それ以外のエラーがあれば、その行を Claude に報告** | |

12-1 のクエリ（直近 6 時間に実行された関数と、その回数・失敗数）:

```kusto
requests
| where timestamp > ago(6h)
| summarize 実行回数 = count(), 失敗 = countif(success == false) by 関数名 = operation_Name
| order by 関数名 asc
```

12-1 の表で、関数名の列に出てくるはずのもの（例）:

| 出てくるはずの関数名 | どの STEP で動いたか |
|---|---|
| `HttpHello`、`HttpRouteParameter`、`HttpPostJson`、`HttpFunctionKey`、`HttpThrow` | STEP 1〜3 |
| `HttpToQueue`、`QueueTrigger`、`QueueTriggerSdkType`、`QueuePoisonTrigger` | STEP 5 |
| `BlobUpload`、`BlobRead`、`BlobList` | STEP 6 |
| `TableInsert`、`TableQuery`、`TableGetOne` | STEP 7 |
| `TimerFromSetting`、`TimerHourly` | STEP 8 |
| `DurableStart`、`ChainingOrchestrator`、`SayHello`、`Square` など | STEP 9 |
| `EventGridCloudEventTrigger`、`EventGridSchemaTrigger` | STEP 10 |
| `McpAdd` など | STEP 11 |

12-3 のクエリ（直近 6 時間の例外を、内容ごとにまとめる）:

```kusto
exceptions
| where timestamp > ago(6h)
| summarize 件数 = count() by 関数名 = operation_Name, 内容 = outerMessage
| order by 件数 desc
```

**STEP 12 まで ○ なら、Azure 上で確認できる範囲の検証は完了です。**

---

## 困ったとき

× が出たら、次の情報をそろえて Claude に報告してください（手書きの要約でかまいません。キーや接続文字列は伏せてください）。

1. どの STEP の何番が × だったか
2. 返ってきたステータス コードと本文（`.http` の応答画面、またはブラウザーの表示）
3. 次の「エラーだけを見るクエリ」の結果の**最初の数件**

```kusto
union traces, exceptions
| where timestamp > ago(30m)
| where severityLevel >= 3 or itemType == "exception"
| where coalesce(innermostMessage, message) !contains "disposed"
| project timestamp, operation_Name, itemType, message, outerMessage, innermostMessage
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
| ログが 0 件 | 届くまで 2〜5 分待つ。クエリの `ago(30m)` を `ago(3h)` に広げる |
