using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace FunctionsTest.Functions.CosmosDb;

/// <summary>
/// Cosmos DB トリガー（変更フィード）/ 入力 / 出力のパターン集（Cosmos DB エミュレーターで動作）。
/// データベース SampleDb、コンテナー Items（パーティションキー /category）を自動作成する。
/// </summary>
public sealed class CosmosDbFunctions(ILogger<CosmosDbFunctions> logger)
{
    /// <summary>Cosmos DB 出力: POST された本文で 1 件作成する。</summary>
    [Function(nameof(CosmosDbCreateItem))]
    public async Task<CosmosDbCreateOutput> CosmosDbCreateItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "cosmos/{category}")] HttpRequest req,
        string category)
    {
        var text = await new StreamReader(req.Body).ReadToEndAsync();
        var item = new CosmosItem(Guid.NewGuid().ToString("N"), category, text);
        return new CosmosDbCreateOutput
        {
            HttpResponse = new OkObjectResult(item),
            Item = item,
        };
    }

    /// <summary>Cosmos DB 入力: id とパーティションキーで 1 件取得する。</summary>
    [Function(nameof(CosmosDbGetItem))]
    public IActionResult CosmosDbGetItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "cosmos/{category}/{id}")] HttpRequest req,
        [CosmosDBInput(CosmosNames.Database, CosmosNames.Container, Connection = "CosmosDbConnection", Id = "{id}", PartitionKey = "{category}")]
        CosmosItem? item)
    {
        return item is null ? new NotFoundResult() : new OkObjectResult(item);
    }

    /// <summary>
    /// 変更フィード トリガー。リース コンテナー leases も自動作成する。
    /// </summary>
    [Function(nameof(CosmosDbChangeFeedTrigger))]
    public void CosmosDbChangeFeedTrigger(
        [CosmosDBTrigger(CosmosNames.Database, CosmosNames.Container, Connection = "CosmosDbConnection",
            LeaseContainerName = "leases", CreateLeaseContainerIfNotExists = true)]
        IReadOnlyList<CosmosItem> changes)
    {
        logger.LogInformation("CosmosDbChangeFeedTrigger: {Count} 件の変更", changes.Count);
        foreach (var c in changes)
        {
            logger.LogInformation("  id={Id}, category={Category}, text={Text}", c.Id, c.Category, c.Text);
        }
    }
}

/// <summary>Cosmos DB に保存するドキュメント。</summary>
public sealed record CosmosItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("text")] string Text);

/// <summary>HTTP 応答 + Cosmos DB 出力。</summary>
public sealed class CosmosDbCreateOutput
{
    [HttpResult]
    public required IActionResult HttpResponse { get; init; }

    [CosmosDBOutput(CosmosNames.Database, CosmosNames.Container, Connection = "CosmosDbConnection",
        CreateIfNotExists = true, PartitionKey = "/category")]
    public CosmosItem? Item { get; init; }
}

/// <summary>Cosmos DB のデータベース名 / コンテナー名（属性引数に使うため const）。</summary>
internal static class CosmosNames
{
    public const string Database = "SampleDb";
    public const string Container = "Items";
}
