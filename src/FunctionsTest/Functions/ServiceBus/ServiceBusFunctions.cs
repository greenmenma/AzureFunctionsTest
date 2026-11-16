using Azure.Messaging.ServiceBus;
using FunctionsTest.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace FunctionsTest.Functions.ServiceBus;

/// <summary>
/// Service Bus トリガー / 出力のパターン集（Service Bus エミュレーターで動作）。
/// エンティティ定義は emulators/servicebus/Config.json を参照。
/// </summary>
public sealed class ServiceBusFunctions(ILogger<ServiceBusFunctions> logger)
{
    /// <summary>HTTP → Service Bus キューへ送信（出力バインド）。</summary>
    [Function(nameof(ServiceBusSend))]
    public async Task<ServiceBusSendOutput> ServiceBusSend(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "servicebus")] HttpRequest req)
    {
        var text = await new StreamReader(req.Body).ReadToEndAsync();
        var message = SampleMessage.Create(string.IsNullOrWhiteSpace(text) ? "from http" : text);
        return new ServiceBusSendOutput
        {
            HttpResponse = new AcceptedResult(location: null, value: message),
            QueueMessage = message,
            TopicMessage = message,
        };
    }

    /// <summary>
    /// キュー トリガー（AutoCompleteMessages = false で手動 settle）。
    /// 本文に "fail" を含むとデッドレターへ送る。それ以外は Complete。
    /// </summary>
    [Function(nameof(ServiceBusQueueTrigger))]
    public async Task ServiceBusQueueTrigger(
        [ServiceBusTrigger("sample-queue", Connection = "ServiceBusConnection", AutoCompleteMessages = false)]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions)
    {
        var body = message.Body.ToString();
        logger.LogInformation("ServiceBusQueueTrigger: messageId={MessageId}, deliveryCount={DeliveryCount}, body={Body}",
            message.MessageId, message.DeliveryCount, body);

        if (body.Contains("fail", StringComparison.OrdinalIgnoreCase))
        {
            await messageActions.DeadLetterMessageAsync(message,
                deadLetterReason: "IntentionalFailure",
                deadLetterErrorDescription: "本文に fail が含まれていたため");
            return;
        }

        await messageActions.CompleteMessageAsync(message);
    }

    /// <summary>デッドレター サブキューを監視する。</summary>
    [Function(nameof(ServiceBusDeadLetterTrigger))]
    public void ServiceBusDeadLetterTrigger(
        [ServiceBusTrigger("sample-queue/$DeadLetterQueue", Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message)
    {
        logger.LogWarning("ServiceBusDeadLetterTrigger: reason={Reason}, description={Description}",
            message.DeadLetterReason, message.DeadLetterErrorDescription);
    }

    /// <summary>トピック / サブスクリプション トリガー（バッチ受信、POCO 配列）。</summary>
    [Function(nameof(ServiceBusTopicTrigger))]
    public void ServiceBusTopicTrigger(
        [ServiceBusTrigger("sample-topic", "sample-subscription", Connection = "ServiceBusConnection", IsBatched = true)]
        SampleMessage[] messages)
    {
        logger.LogInformation("ServiceBusTopicTrigger: {Count} 件受信 ids={Ids}",
            messages.Length, string.Join(",", messages.Select(m => m.Id)));
    }
}

/// <summary>HTTP 応答 + キュー / トピックへの同時送信。</summary>
public sealed class ServiceBusSendOutput
{
    [HttpResult]
    public required IActionResult HttpResponse { get; init; }

    [ServiceBusOutput("sample-queue", Connection = "ServiceBusConnection")]
    public SampleMessage? QueueMessage { get; init; }

    [ServiceBusOutput("sample-topic", ServiceBusEntityType.Topic, Connection = "ServiceBusConnection")]
    public SampleMessage? TopicMessage { get; init; }
}
