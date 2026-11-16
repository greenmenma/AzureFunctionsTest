using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Client.Entities;
using Microsoft.DurableTask.Entities;
using Microsoft.Extensions.Logging;

namespace FunctionsTest.Functions.Durable;

/// <summary>
/// Durable Functions の起動用 HTTP エンドポイント集。
/// 状態は AzureWebJobsStorage（Azurite）に保存される。
/// </summary>
public sealed class DurableStarters(ILogger<DurableStarters> logger)
{
    /// <summary>
    /// 任意のオーケストレーションを名前指定で起動する。
    /// 例: POST /api/durable/start/ChainingOrchestrator
    /// 応答の statusQueryGetUri で進捗を確認できる。
    /// </summary>
    [Function(nameof(DurableStart))]
    public async Task<HttpResponseData> DurableStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "durable/start/{orchestratorName}")] HttpRequestData req,
        string orchestratorName,
        [DurableClient] DurableTaskClient client)
    {
        var input = await req.ReadAsStringAsync();
        var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(orchestratorName,
            string.IsNullOrWhiteSpace(input) ? null : input);
        logger.LogInformation("DurableStart: {Orchestrator} を起動 instanceId={InstanceId}", orchestratorName, instanceId);
        return await client.CreateCheckStatusResponseAsync(req, instanceId);
    }

    /// <summary>
    /// 承認待ちオーケストレーションへ外部イベント "Approval" を送る。
    /// 例: POST /api/durable/approve/{instanceId}?approved=true
    /// </summary>
    [Function(nameof(DurableApprove))]
    public async Task<IActionResult> DurableApprove(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "durable/approve/{instanceId}")] HttpRequest req,
        string instanceId,
        [DurableClient] DurableTaskClient client)
    {
        var approved = !bool.TryParse(req.Query["approved"], out var a) || a;
        await client.RaiseEventAsync(instanceId, ApprovalOrchestration.EventName, approved);
        return new AcceptedResult(location: null, value: new { instanceId, approved });
    }

    /// <summary>
    /// エンティティ操作。例: POST /api/durable/counter/{key}/add?value=5, /reset
    /// </summary>
    [Function(nameof(DurableCounterSignal))]
    public async Task<IActionResult> DurableCounterSignal(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "durable/counter/{key}/{operation}")] HttpRequest req,
        string key,
        string operation,
        [DurableClient] DurableTaskClient client)
    {
        var entityId = new EntityInstanceId(nameof(CounterEntity), key);
        int? value = int.TryParse(req.Query["value"], out var v) ? v : null;
        await client.Entities.SignalEntityAsync(entityId, operation, value);
        return new AcceptedResult(location: null, value: new { entity = entityId.ToString(), operation, value });
    }

    /// <summary>エンティティ状態の取得。例: GET /api/durable/counter/{key}</summary>
    [Function(nameof(DurableCounterGet))]
    public async Task<IActionResult> DurableCounterGet(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "durable/counter/{key}")] HttpRequest req,
        string key,
        [DurableClient] DurableTaskClient client)
    {
        EntityMetadata<int>? entity = await client.Entities.GetEntityAsync<int>(new EntityInstanceId(nameof(CounterEntity), key));
        return entity is null ? new NotFoundResult() : new OkObjectResult(new { key, value = entity.State });
    }
}
