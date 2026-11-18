# 次にやること（発行から関数一覧の確認まで）

管理者が作成した Flex Consumption の関数アプリに、修正済みのプロジェクトを発行し、関数一覧に表示されるところまでの手順です。
**上から順に**進めてください。Azure CLI（az コマンド）は使いません。

各手順の「結果」欄に、見えた内容を記入してください。

---

## これまでに分かったこと

| 症状 | 原因 | 対応 |
|---|---|---|
| 関数一覧が空、`/api/hello` が 503 | Application Insights のパッケージのバージョンが混在し、関数を実行するプロセスが起動直後に落ちていた（`Exceeded language worker restart retry count`） | プロジェクトを修正済み（`Microsoft.ApplicationInsights.WorkerService` を 2.23.0 に変更）。**再発行が必要** |
| `EventHub account connection string with name 'EventHubsConnection' does not exist` | Azure に用意していないサービスの接続文字列の設定がない（無効にした関数でも確認される） | ダミー値のアプリ設定を追加する（手順 3） |

---

## 1. 最新のプロジェクトを取得する

| # | やること | 確認 | 結果 |
|---|---|---|---|
| 1-1 | リポジトリのフォルダーで `git pull` を実行する（VS2026 なら「Git」→「プル」） | エラーなく完了する | |
| 1-2 | `src/FunctionsTest/FunctionsTest.csproj` を開く | `Microsoft.ApplicationInsights.WorkerService` の `Version` が **`2.23.0`** になっている（`3.2.0` のままなら 1-1 が反映されていない） | |

## 2. 発行する

| # | やること | 確認 | 結果 |
|---|---|---|---|
| 2-1 | VS2026 で `AzureFunctionsTest.slnx` を開き、「ビルド」→「ソリューションのリビルド」 | 「リビルド: 成功 1」 | |
| 2-2 | ソリューション エクスプローラーで `FunctionsTest` を右クリック →「発行」 | 以前作った発行プロファイルが表示される | |
| 2-3 | 発行プロファイルの設定を確認する | 構成 = **Release**、ターゲット フレームワーク = **net10.0**、発行先 = **管理者が作成した関数アプリ** | |
| 2-4 | 「発行」をクリック | 「出力」ウィンドウの最後に「発行が成功しました」/「Publish Succeeded」 | |

> 発行プロファイルがない場合は、「発行」→「ターゲット: Azure」→「Azure Function App」→ サブスクリプションと関数アプリを選択 →「完了」で作成します。

## 3. アプリ設定を追加する

Azure ポータル → 関数アプリ →「設定」→「環境変数」→「アプリ設定」タブ

| # | やること | 確認 | 結果 |
|---|---|---|---|
| 3-1 | 以前 [appsettings/isolate-http-only.json](appsettings/isolate-http-only.json) の設定を追加していた場合は、その 37 件（`AzureWebJobs.ApprovalOrchestrator.Disabled` など）を削除する | 追加していなければ何もしない | |
| 3-2 | 「詳細設定の編集」を開く | 既存の設定が JSON の配列で表示される | |
| 3-3 | **既存の内容は消さずに**、最後の `}` の後ろに `,` を付け、[appsettings/flex-basic.json](appsettings/flex-basic.json) の `[` と `]` の間の行（31 行）を貼り付ける | 同じ名前の設定が既にある場合は、重複させずに値だけ合わせる | |
| 3-4 | 貼り付けた `StorageConnection` の値 `<AzureWebJobsStorage と同じ値に置き換える>` を、`AzureWebJobsStorage` の値に書き換える | `AzureWebJobsStorage` がなく `AzureWebJobsStorage__accountName` しかない場合は、名前を `StorageConnection__accountName` に変えて同じ値にする | |
| 3-5 | 「OK」→ 画面下の「適用」→「確認」 | 保存されたメッセージが出る | |
| 3-6 | 「アプリ設定」タブの一覧で、`StorageConnection`・`CosmosDbConnection`・`KafkaBrokerList`・`AzureWebJobs.BlobTrigger.Disabled` を検索する | 4 つとも表示される。**「接続文字列」タブに入っていないこと**（そこに入れた値は別名の環境変数になり、関数から参照できない）。名前の大文字・小文字も一致していること | |

`flex-basic.json` で追加される設定:

- `StorageConnection` / `TimerSchedule` / `KafkaBrokerList`
- 接続文字列のダミー値 6 件（`ServiceBusConnection` / `EventHubsConnection` / `CosmosDbConnection` / `SqlConnection` / `RabbitMQConnection` / `SignalRConnection`）
- Azure に用意していないサービスの関数と、Flex Consumption で使えない `BlobTrigger` を無効にする設定 22 件

## 4. 再起動して確認する

| # | やること | 確認 | 結果 |
|---|---|---|---|
| 4-1 | 「概要」→「再起動」 | 再起動の完了メッセージが出る | |
| 4-2 | 1〜2 分待ってから、ブラウザーで `https://<既定のドメイン>/api/hello?name=test` を開く | **`{"message":"Hello, test!"}`** が返る（503 なら 30 秒ほど待って再読み込み） | |
| 4-3 | 「概要」の下部（または左メニューの「関数」）を開き、ページを再読み込みする | **60 個の関数**が表示される（無効にした 22 個は「無効」と表示される） | |

**4-2 と 4-3 がどちらも期待どおりなら完了です。** 「6. 表示された後」に進んでください。

## 5. まだ表示されない場合

[application-insights-guide.md](application-insights-guide.md) の手順で、Application Insights のログを確認します。

1. 「概要」→「再起動」→ 1 分待って `/api/hello` を 1 回開く → 2〜5 分待つ
2. Application Insights →「ログ」（KQL モード）で次のクエリを実行する

```kusto
union traces, exceptions
| where timestamp > ago(30m)
| where severityLevel >= 3 or itemType == "exception"
| where coalesce(innermostMessage, message) !contains "disposed"
| project timestamp, itemType, message, outerMessage, innermostMessage
| order by timestamp asc
| take 30
```

3. 次の内容を Claude に報告する（手書きの要約でよい）
   - 2-4 の発行結果、1-2 のバージョン
   - 4-2 で返ってきた内容（ステータス コード）
   - クエリ結果の**最初の数件**の `message` / `innermostMessage`（特に `Exception`、`Could not load`、`does not exist`、`Exceeded` を含むもの）

## 6. 表示された後（動作確認）

ブラウザーで次の URL を開くと、関数が動いているかを簡単に確認できます。

| URL（`https://<既定のドメイン>` に続ける） | 期待値 |
|---|---|
| `/api/hello?name=test` | `{"message":"Hello, test!"}` |
| `/api/items/42/books` | `id` と `category` を含む JSON |
| `/api/items/abc` | 404（ルート制約違反） |
| `/api/throw` | 500（Application Insights の「失敗」→「例外」に記録される） |

VS2026 の `http/FunctionsTest.http` からまとめて呼び出す場合は、`http/http-client.env.json.user` を手で作ります（Git の管理対象外なので、キーを書いてもコミットされません）。
キーは、ポータル → 関数アプリ →「関数」→「アプリ キー」で確認できます。

```json
{
  "azure": {
    "host": "https://<既定のドメイン>",
    "functionKey": "<ホスト キーの default の値>",
    "eventGridKey": "<システム キーの eventgrid_extension の値>",
    "mcpKey": "<システム キーの mcp_extension の値>"
  }
}
```

保存したら、VS2026 で `FunctionsTest.http` を開き、右上の環境を **azure** に切り替えて各リクエストを送信します。
