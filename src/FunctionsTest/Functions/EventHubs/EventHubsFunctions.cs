using Azure.Messaging.EventHubs;
using FunctionsTest.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace FunctionsTest.Functions.EventHubs;

/// <summary>
/// Event Hubs トリガー / 出力のパターン集（Event Hubs エミュレーターで動作）。
/// チェックポイントは AzureWebJobsStorage（Azurite）に保存される。
/// </summary>
public sealed class EventHubsFunctions(ILogger<EventHubsFunctions> logger)
{
    /// <summary>HTTP → Event Hub へ複数イベントを送信。?count=N で件数を指定。</summary>
    [Function(nameof(EventHubSend))]
    public EventHubSendOutput EventHubSend(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "eventhubs")] HttpRequest req)
    {
        var count = int.TryParse(req.Query["count"], out var n) ? Math.Clamp(n, 1, 100) : 3;
        var events = Enumerable.Range(1, count).Select(i => SampleMessage.Create($"event #{i}")).ToArray();
        return new EventHubSendOutput
        {
            HttpResponse = new AcceptedResult(location: null, value: new { sent = events.Length }),
            Events = events,
        };
    }

    /// <summary>バッチ受信（既定）。SDK 型 EventData[] でパーティション情報も確認する。</summary>
    [Function(nameof(EventHubBatchTrigger))]
    public void EventHubBatchTrigger(
        [EventHubTrigger("sample-hub", Connection = "EventHubsConnection", ConsumerGroup = "$Default")]
        EventData[] events,
        FunctionContext context)
    {
        context.BindingContext.BindingData.TryGetValue("PartitionContext", out var partition);
        logger.LogInformation("EventHubBatchTrigger: {Count} 件受信 partition={Partition}", events.Length, partition);
        foreach (var e in events)
        {
            logger.LogInformation("  seq={Seq}, enqueued={Enqueued}, body={Body}",
                e.SequenceNumber, e.EnqueuedTime, e.EventBody.ToString());
        }
    }
}

/// <summary>HTTP 応答 + Event Hub 出力（配列で複数イベント）。</summary>
public sealed class EventHubSendOutput
{
    [HttpResult]
    public required IActionResult HttpResponse { get; init; }

    [EventHubOutput("sample-hub", Connection = "EventHubsConnection")]
    public SampleMessage[]? Events { get; init; }
}
