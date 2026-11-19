using Azure;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace FunctionsTest.Functions.Storage;

/// <summary>
/// Table Storage の入力 / 出力パターン集（Table にはトリガーはない）。
/// </summary>
public sealed class TableFunctions(ILogger<TableFunctions> logger)
{
    /// <summary>Table 出力: POST された本文を 1 行追加する。</summary>
    [Function(nameof(TableInsert))]
    public async Task<TableInsertOutput> TableInsert(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tables/{partitionKey}")] HttpRequest req,
        string partitionKey)
    {
        var text = await new StreamReader(req.Body).ReadToEndAsync();
        var entity = new SampleEntity
        {
            PartitionKey = partitionKey,
            RowKey = Guid.NewGuid().ToString("N"),
            Text = text,
        };
        logger.LogInformation("TableInsert: pk={PartitionKey}, rk={RowKey}", entity.PartitionKey, entity.RowKey);

        return new TableInsertOutput
        {
            HttpResponse = new OkObjectResult(new { entity.PartitionKey, entity.RowKey, entity.Text }),
            Entity = entity,
        };
    }

    /// <summary>
    /// Table 入力（1 件）: PartitionKey / RowKey を指定して取得する。
    /// ※ TableInput で POCO（SampleEntity）に直接バインドする書き方は Azure 上で 500 になったため、
    ///   TableClient を受け取り、存在しない場合も例外にならない GetEntityIfExistsAsync で取得する。
    /// </summary>
    [Function(nameof(TableGetOne))]
    public async Task<IActionResult> TableGetOne(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tables/{partitionKey}/{rowKey}")] HttpRequest req,
        string partitionKey,
        string rowKey,
        [TableInput("SampleTable", Connection = "StorageConnection")] TableClient table)
    {
        var response = await table.GetEntityIfExistsAsync<SampleEntity>(partitionKey, rowKey);
        if (!response.HasValue || response.Value is null)
        {
            return new NotFoundObjectResult(new { error = $"{partitionKey}/{rowKey} は存在しません" });
        }

        var entity = response.Value;
        return new OkObjectResult(new { entity.PartitionKey, entity.RowKey, entity.Text, entity.Timestamp });
    }

    /// <summary>Table 入力（SDK 型 TableClient）: パーティション内を一覧する。</summary>
    [Function(nameof(TableQuery))]
    public async Task<IActionResult> TableQuery(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tables/{partitionKey}")] HttpRequest req,
        string partitionKey,
        [TableInput("SampleTable", Connection = "StorageConnection")] TableClient table)
    {
        await table.CreateIfNotExistsAsync();
        var rows = new List<object>();
        await foreach (var e in table.QueryAsync<SampleEntity>(e => e.PartitionKey == partitionKey))
        {
            rows.Add(new { e.PartitionKey, e.RowKey, e.Text });
        }
        return new OkObjectResult(rows);
    }
}

/// <summary>Table に保存するエンティティ。</summary>
public sealed class SampleEntity : ITableEntity
{
    public string PartitionKey { get; set; } = default!;
    public string RowKey { get; set; } = default!;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
    public string? Text { get; set; }
}

/// <summary>HTTP 応答と Table 出力を同時に返すための型。</summary>
public sealed class TableInsertOutput
{
    [HttpResult]
    public required IActionResult HttpResponse { get; init; }

    [TableOutput("SampleTable", Connection = "StorageConnection")]
    public SampleEntity? Entity { get; init; }
}
