using Azure.Messaging;
using Azure.Messaging.EventGrid;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace FunctionsTest.Functions.EventGrid;

/// <summary>
/// Event Grid トリガーのパターン集。
/// Event Grid には公式エミュレーターがないため、ローカルでは Webhook エンドポイント
///   POST http://localhost:7071/runtime/webhooks/EventGrid?functionName={関数名}
/// にヘッダー "aeg-event-type: Notification" を付けて直接 POST して確認する（http/FunctionsTest.http 参照）。
/// </summary>
public sealed class EventGridFunctions(ILogger<EventGridFunctions> logger)
{
    /// <summary>CloudEvents v1.0 スキーマで受け取る（推奨）。</summary>
    [Function(nameof(EventGridCloudEventTrigger))]
    public void EventGridCloudEventTrigger([EventGridTrigger] CloudEvent cloudEvent)
    {
        logger.LogInformation("EventGridCloudEventTrigger: type={Type}, source={Source}, subject={Subject}, data={Data}",
            cloudEvent.Type, cloudEvent.Source, cloudEvent.Subject, cloudEvent.Data?.ToString());
    }

    /// <summary>Event Grid 独自スキーマで受け取る。</summary>
    [Function(nameof(EventGridSchemaTrigger))]
    public void EventGridSchemaTrigger([EventGridTrigger] EventGridEvent eventGridEvent)
    {
        logger.LogInformation("EventGridSchemaTrigger: eventType={EventType}, subject={Subject}, data={Data}",
            eventGridEvent.EventType, eventGridEvent.Subject, eventGridEvent.Data?.ToString());
    }
}
