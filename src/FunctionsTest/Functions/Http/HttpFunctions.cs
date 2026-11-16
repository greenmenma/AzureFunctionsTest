using System.Text.Json;
using FunctionsTest.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace FunctionsTest.Functions.Http;

/// <summary>
/// HTTP トリガーのパターン集（ASP.NET Core 統合モデル）。
/// </summary>
public sealed class HttpFunctions(ILogger<HttpFunctions> logger)
{
    /// <summary>GET + クエリ文字列。認証レベル Anonymous。</summary>
    [Function(nameof(HttpHello))]
    public IActionResult HttpHello(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "hello")] HttpRequest req)
    {
        var name = req.Query["name"].FirstOrDefault() ?? "world";
        logger.LogInformation("HttpHello: name={Name}", name);
        return new OkObjectResult(new { message = $"Hello, {name}!" });
    }

    /// <summary>ルートパラメーター（型制約付き）。</summary>
    [Function(nameof(HttpRouteParameter))]
    public IActionResult HttpRouteParameter(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "items/{id:int}/{category?}")] HttpRequest req,
        int id,
        string? category)
    {
        return new OkObjectResult(new { id, category = category ?? "(none)" });
    }

    /// <summary>POST + JSON 本文。不正な JSON なら 400 を返す。</summary>
    [Function(nameof(HttpPostJson))]
    public async Task<IActionResult> HttpPostJson(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "echo")] HttpRequest req)
    {
        try
        {
            var body = await req.ReadFromJsonAsync<SampleMessage>();
            if (body is null || string.IsNullOrWhiteSpace(body.Text))
            {
                return new BadRequestObjectResult(new { error = "text は必須です" });
            }
            return new OkObjectResult(body);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "HttpPostJson: JSON の解析に失敗");
            return new BadRequestObjectResult(new { error = "JSON の形式が不正です" });
        }
    }

    /// <summary>認証レベル Function。ローカルではキー不要、Azure 上では ?code= が必要。</summary>
    [Function(nameof(HttpFunctionKey))]
    public IActionResult HttpFunctionKey(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "secure")] HttpRequest req)
    {
        return new OkObjectResult(new { message = "関数キーで認証されました" });
    }

    /// <summary>
    /// 複数出力: HTTP 応答を返しつつ、Queue へメッセージを書き込む。
    /// 書き込まれたメッセージは QueueFunctions.QueueTrigger が処理する。
    /// </summary>
    [Function(nameof(HttpToQueue))]
    public async Task<HttpToQueueOutput> HttpToQueue(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "enqueue")] HttpRequest req)
    {
        var text = await new StreamReader(req.Body).ReadToEndAsync();
        var message = SampleMessage.Create(string.IsNullOrWhiteSpace(text) ? "from http" : text);
        logger.LogInformation("HttpToQueue: id={Id}", message.Id);

        return new HttpToQueueOutput
        {
            HttpResponse = new AcceptedResult(location: null, value: message),
            QueueMessage = message,
        };
    }

    /// <summary>意図的に例外を投げ、ミドルウェアのエラーログと 500 応答を確認する。</summary>
    [Function(nameof(HttpThrow))]
    public IActionResult HttpThrow(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "throw")] HttpRequest req)
    {
        throw new InvalidOperationException("意図的な例外です");
    }
}

/// <summary>HTTP 応答と Queue 出力を同時に返すための型。</summary>
public sealed class HttpToQueueOutput
{
    [HttpResult]
    public required IActionResult HttpResponse { get; init; }

    [QueueOutput("sample-queue", Connection = "StorageConnection")]
    public SampleMessage? QueueMessage { get; init; }
}
