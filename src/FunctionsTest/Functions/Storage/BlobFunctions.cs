using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace FunctionsTest.Functions.Storage;

/// <summary>
/// Blob トリガー / 入力 / 出力のパターン集（Azurite で動作）。
/// </summary>
public sealed class BlobFunctions(ILogger<BlobFunctions> logger)
{
    /// <summary>
    /// HTTP で受けた本文を uploads/{name} に保存する（Blob 出力）。
    /// 保存されると BlobTrigger が起動する。
    /// </summary>
    [Function(nameof(BlobUpload))]
    public async Task<BlobUploadOutput> BlobUpload(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "blobs/{name}")] HttpRequest req,
        string name)
    {
        var content = await new StreamReader(req.Body).ReadToEndAsync();
        return new BlobUploadOutput
        {
            HttpResponse = new OkObjectResult(new { saved = $"uploads/{name}", length = content.Length }),
            Content = content,
        };
    }

    /// <summary>
    /// Blob トリガー（既定のポーリング方式）。
    /// 文字列で受け取り、名前はバインド式 {name} から取得する。
    /// ※ Flex Consumption では Source = BlobTriggerSource.EventGrid が必須。
    /// </summary>
    [Function(nameof(BlobTrigger))]
    public void BlobTrigger(
        [BlobTrigger("uploads/{name}", Connection = "StorageConnection")] string content,
        string name)
    {
        logger.LogInformation("BlobTrigger: name={Name}, length={Length}", name, content.Length);
    }

    /// <summary>
    /// Blob 入力（SDK 型 BlobClient）。uploads/{name} の中身とプロパティを返す。
    /// </summary>
    [Function(nameof(BlobRead))]
    public async Task<IActionResult> BlobRead(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "blobs/{name}")] HttpRequest req,
        [BlobInput("uploads/{name}", Connection = "StorageConnection")] BlobClient blob)
    {
        if (!await blob.ExistsAsync())
        {
            return new NotFoundObjectResult(new { error = $"{blob.Name} は存在しません" });
        }

        var props = await blob.GetPropertiesAsync();
        var download = await blob.DownloadContentAsync();
        return new OkObjectResult(new
        {
            name = blob.Name,
            contentLength = props.Value.ContentLength,
            lastModified = props.Value.LastModified,
            content = download.Value.Content.ToString(),
        });
    }

    /// <summary>
    /// コンテナー単位の入力（BlobContainerClient）で一覧を返す。
    /// </summary>
    [Function(nameof(BlobList))]
    public async Task<IActionResult> BlobList(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "blobs")] HttpRequest req,
        [BlobInput("uploads", Connection = "StorageConnection")] BlobContainerClient container)
    {
        await container.CreateIfNotExistsAsync();
        var names = new List<string>();
        await foreach (var item in container.GetBlobsAsync())
        {
            names.Add(item.Name);
        }
        return new OkObjectResult(names);
    }
}

/// <summary>HTTP 応答と Blob 出力を同時に返すための型。</summary>
public sealed class BlobUploadOutput
{
    [HttpResult]
    public required IActionResult HttpResponse { get; init; }

    [BlobOutput("uploads/{name}", Connection = "StorageConnection")]
    public string? Content { get; init; }
}
