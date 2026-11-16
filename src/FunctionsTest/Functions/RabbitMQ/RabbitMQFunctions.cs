using FunctionsTest.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace FunctionsTest.Functions.RabbitMQ;

/// <summary>
/// RabbitMQ トリガー / 出力のパターン集（RabbitMQ コンテナーで動作）。
/// ※ Azure 上では Premium / Dedicated プランでのみ利用可能（Consumption / Flex は不可）。
/// </summary>
public sealed class RabbitMQFunctions(ILogger<RabbitMQFunctions> logger)
{
    /// <summary>HTTP → RabbitMQ キューへ送信。</summary>
    [Function(nameof(RabbitMQSend))]
    public async Task<RabbitMQSendOutput> RabbitMQSend(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "rabbitmq")] HttpRequest req)
    {
        var text = await new StreamReader(req.Body).ReadToEndAsync();
        var message = SampleMessage.Create(string.IsNullOrWhiteSpace(text) ? "from http" : text);
        return new RabbitMQSendOutput
        {
            HttpResponse = new AcceptedResult(location: null, value: message),
            Message = message,
        };
    }

    /// <summary>キュー トリガー（POCO で受け取る）。</summary>
    [Function(nameof(RabbitMQTrigger))]
    public void RabbitMQTrigger(
        [RabbitMQTrigger("sample-queue", ConnectionStringSetting = "RabbitMQConnection")] SampleMessage message)
    {
        logger.LogInformation("RabbitMQTrigger: id={Id}, text={Text}", message.Id, message.Text);
    }
}

/// <summary>HTTP 応答 + RabbitMQ 出力。</summary>
public sealed class RabbitMQSendOutput
{
    [HttpResult]
    public required IActionResult HttpResponse { get; init; }

    [RabbitMQOutput(QueueName = "sample-queue", ConnectionStringSetting = "RabbitMQConnection")]
    public SampleMessage? Message { get; init; }
}
