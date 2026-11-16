using FunctionsTest.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace FunctionsTest.Functions.Kafka;

/// <summary>
/// Kafka トリガー / 出力のパターン集（Apache Kafka コンテナーで動作）。
/// ブローカーはアプリ設定 KafkaBrokerList から読む。
/// ※ Azure 上では Premium / Dedicated / Flex Consumption プランでのみ利用可能。
/// </summary>
public sealed class KafkaFunctions(ILogger<KafkaFunctions> logger)
{
    private const string Topic = "sample-topic";

    /// <summary>HTTP → Kafka トピックへ送信。</summary>
    [Function(nameof(KafkaSend))]
    public async Task<KafkaSendOutput> KafkaSend(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "kafka")] HttpRequest req)
    {
        var text = await new StreamReader(req.Body).ReadToEndAsync();
        var message = SampleMessage.Create(string.IsNullOrWhiteSpace(text) ? "from http" : text);
        return new KafkaSendOutput
        {
            HttpResponse = new AcceptedResult(location: null, value: message),
            Message = System.Text.Json.JsonSerializer.Serialize(message),
        };
    }

    /// <summary>
    /// バッチ受信。各要素は Offset / Partition / Topic / Timestamp / Value を含む JSON 文字列で渡される。
    /// </summary>
    [Function(nameof(KafkaTrigger))]
    public void KafkaTrigger(
        [KafkaTrigger("%KafkaBrokerList%", Topic, ConsumerGroup = "functions-test", IsBatched = true)]
        string[] events)
    {
        logger.LogInformation("KafkaTrigger: {Count} 件受信", events.Length);
        foreach (var e in events)
        {
            logger.LogInformation("  {Event}", e);
        }
    }
}

/// <summary>HTTP 応答 + Kafka 出力。</summary>
public sealed class KafkaSendOutput
{
    [HttpResult]
    public required IActionResult HttpResponse { get; init; }

    [KafkaOutput("%KafkaBrokerList%", "sample-topic")]
    public string? Message { get; init; }
}
