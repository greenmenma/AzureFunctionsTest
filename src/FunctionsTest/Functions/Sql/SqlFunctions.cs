using System.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Sql;
using Microsoft.Extensions.Logging;

namespace FunctionsTest.Functions.Sql;

/// <summary>
/// Azure SQL / SQL Server のトリガー / 入力 / 出力パターン集（SQL Server コンテナーで動作）。
/// テーブルと変更の追跡は emulators/sql/init.sql で作成する。
/// </summary>
public sealed class SqlFunctions(ILogger<SqlFunctions> logger)
{
    /// <summary>SQL 出力: 主キーが一致すれば更新、なければ挿入（upsert）。</summary>
    [Function(nameof(SqlInsertItem))]
    public async Task<SqlInsertOutput> SqlInsertItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "sql/{category}")] HttpRequest req,
        string category)
    {
        var text = await new StreamReader(req.Body).ReadToEndAsync();
        var item = new SqlItem { Id = Guid.NewGuid(), Category = category, Text = text };
        return new SqlInsertOutput
        {
            HttpResponse = new OkObjectResult(item),
            Item = item,
        };
    }

    /// <summary>SQL 入力: パラメーター付きクエリ（ルート値をバインド）。</summary>
    [Function(nameof(SqlGetItems))]
    public IActionResult SqlGetItems(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "sql/{category}")] HttpRequest req,
        [SqlInput("SELECT Id, Category, Text FROM dbo.Items WHERE Category = @Category",
            "SqlConnection", CommandType.Text, "@Category={category}")]
        IEnumerable<SqlItem> items)
    {
        return new OkObjectResult(items);
    }

    /// <summary>
    /// SQL トリガー: 変更の追跡（Change Tracking）で dbo.Items の挿入 / 更新 / 削除を検知する。
    /// 状態管理用のリース テーブルは az_func スキーマに自動作成される。
    /// </summary>
    [Function(nameof(SqlTriggerItems))]
    public void SqlTriggerItems(
        [SqlTrigger("[dbo].[Items]", "SqlConnection")] IReadOnlyList<SqlChange<SqlItem>> changes)
    {
        logger.LogInformation("SqlTriggerItems: {Count} 件の変更", changes.Count);
        foreach (var change in changes)
        {
            logger.LogInformation("  {Operation}: id={Id}, category={Category}, text={Text}",
                change.Operation, change.Item.Id, change.Item.Category, change.Item.Text);
        }
    }
}

/// <summary>dbo.Items の行。</summary>
public sealed class SqlItem
{
    public Guid Id { get; set; }
    public string Category { get; set; } = "";
    public string? Text { get; set; }
}

/// <summary>HTTP 応答 + SQL 出力。</summary>
public sealed class SqlInsertOutput
{
    [HttpResult]
    public required IActionResult HttpResponse { get; init; }

    [SqlOutput("dbo.Items", "SqlConnection")]
    public SqlItem? Item { get; init; }
}
