using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace FunctionsTest.Functions.Durable;

/// <summary>
/// パターン1: 関数チェーン。アクティビティを順番に呼ぶ。
/// </summary>
public static class ChainingOrchestration
{
    [Function(nameof(ChainingOrchestrator))]
    public static async Task<List<string>> ChainingOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        // リプレイ時に重複出力しないロガー
        var logger = context.CreateReplaySafeLogger(nameof(ChainingOrchestrator));
        logger.LogInformation("ChainingOrchestrator: 開始");

        var results = new List<string>
        {
            await context.CallActivityAsync<string>(nameof(Activities.SayHello), "Tokyo"),
            await context.CallActivityAsync<string>(nameof(Activities.SayHello), "Osaka"),
            await context.CallActivityAsync<string>(nameof(Activities.SayHello), "Nagoya"),
        };
        return results;
    }
}

/// <summary>
/// パターン2: ファンアウト / ファンイン。並列に実行して結果を集計する。
/// リトライポリシー付きでアクティビティを呼ぶ例も兼ねる。
/// </summary>
public static class FanOutFanInOrchestration
{
    [Function(nameof(FanOutFanInOrchestrator))]
    public static async Task<int> FanOutFanInOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var retry = TaskOptions.FromRetryPolicy(new RetryPolicy(
            maxNumberOfAttempts: 3, firstRetryInterval: TimeSpan.FromSeconds(2)));

        var workItems = await context.CallActivityAsync<int[]>(nameof(Activities.GetWorkItems), 10);
        var tasks = workItems.Select(i => context.CallActivityAsync<int>(nameof(Activities.Square), i, retry));
        var squares = await Task.WhenAll(tasks);
        return squares.Sum();
    }
}

/// <summary>
/// パターン3: 人による承認（外部イベント + タイムアウト）。
/// 30 秒以内に Approval イベントが来なければタイムアウト扱いにする。
/// </summary>
public static class ApprovalOrchestration
{
    public const string EventName = "Approval";

    [Function(nameof(ApprovalOrchestrator))]
    public static async Task<string> ApprovalOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        context.SetCustomStatus("承認待ち");
        try
        {
            var approved = await context.WaitForExternalEvent<bool>(EventName, TimeSpan.FromSeconds(30));
            context.SetCustomStatus(approved ? "承認" : "却下");
            return approved ? "Approved" : "Rejected";
        }
        catch (TaskCanceledException)
        {
            // タイムアウト分岐
            context.SetCustomStatus("タイムアウト");
            return "TimedOut";
        }
    }
}

/// <summary>
/// パターン4: モニター（耐久タイマーによるポーリング）。
/// 5 秒間隔で状態を確認し、条件を満たすか 6 回試したら終了する。
/// </summary>
public static class MonitorOrchestration
{
    [Function(nameof(MonitorOrchestrator))]
    public static async Task<string> MonitorOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        for (var attempt = 1; attempt <= 6; attempt++)
        {
            var done = await context.CallActivityAsync<bool>(nameof(Activities.CheckStatus), attempt);
            context.SetCustomStatus(new { attempt, done });
            if (done)
            {
                return $"完了 (attempt={attempt})";
            }
            await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(5), CancellationToken.None);
        }
        return "諦めました";
    }
}

/// <summary>
/// パターン5: サブオーケストレーション。チェーンを子として呼ぶ。
/// </summary>
public static class SubOrchestration
{
    [Function(nameof(ParentOrchestrator))]
    public static async Task<object> ParentOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var chain = await context.CallSubOrchestratorAsync<List<string>>(nameof(ChainingOrchestration.ChainingOrchestrator));
        var sum = await context.CallSubOrchestratorAsync<int>(nameof(FanOutFanInOrchestration.FanOutFanInOrchestrator));
        return new { chain, sum };
    }
}
