# Application Insights でログを確認する手順

関数アプリの起動エラーや実行時のエラーを、Azure ポータルの Application Insights で確認する手順です。
発行後の確認全体の流れは [publish-checklist.md](publish-checklist.md) を参照してください。

Application Insights は、**有効にした後に起きたこと**しか記録しません。そのため、まず起動をやり直してログを出させてから見ます。

1. **起動をやり直す**: 関数アプリ →「概要」→「再起動」。1 分ほど待ってから、ブラウザーで `https://<既定のドメイン>/api/hello` を 1 回開く（503 でもよい。インスタンスを起こすため）
2. **2〜5 分待つ**: ログが Application Insights に届くまで数分かかる
3. **Application Insights を開く**: 関数アプリの左メニュー「監視」→「Application Insights」→「Application Insights データの表示」
4. **ログ画面を開く**: Application Insights の左メニュー「監視」→「ログ」
   - 最初に「クエリ」のサンプル一覧が出たら、右上の × で閉じる
   - 画面右上の切り替えが「シンプル モード」なら「KQL モード」に変える
5. **クエリを貼り付けて「実行」**: 起動時のログ（エラー以外も含む）を古い順に表示する

```kusto
union traces, exceptions
| where timestamp > ago(30m)
| project timestamp, itemType, severityLevel, message, outerMessage, innermostMessage
| order by timestamp asc
| take 200
```

6. **見るポイント**
   - `severityLevel` が **3（Error）・4（Critical）** の行と、`itemType` が **exception** の行
   - `Starting Host`、`Worker process started`、`Host started`、`Job host stopped` などの起動・停止の流れ
   - 0 件なら、手順 2 の待ち時間が足りないか、まだインスタンスが起動していない（手順 1 からやり直す）
7. **結果を共有する**: 結果の表の上にある「エクスポート」→「CSV（表示列）」でファイルに保存するか、スクリーンショットを撮る。キーや接続文字列が写っていたら伏せる

> クエリを書かずに見たい場合は、Application Insights の左メニュー「調査」→「失敗」→「例外」タブでも、例外の一覧と詳細（スタック トレース）を確認できます。

## よく使うクエリ

**警告・エラーと例外だけを見る**（直近 1 時間、新しい順）

```kusto
union traces, exceptions
| where timestamp > ago(1h)
| where severityLevel >= 2 or itemType == "exception"
| project timestamp, itemType, severityLevel, message, outerMessage, innermostMessage
| order by timestamp desc
| take 50
```

**最初のエラーを探す**（`ObjectDisposedException` などの二次的なエラーを除き、古い順）

```kusto
union traces, exceptions
| where timestamp > ago(24h)
| where severityLevel >= 3 or itemType == "exception"
| where coalesce(innermostMessage, message) !contains "disposed"
| project timestamp, itemType, message, outerMessage, innermostMessage
| order by timestamp asc
| take 30
```

## severityLevel の意味

| 値 | レベル |
|---|---|
| 0 | Verbose（詳細） |
| 1 | Information（情報） |
| 2 | Warning（警告） |
| 3 | Error（エラー） |
| 4 | Critical（重大） |
