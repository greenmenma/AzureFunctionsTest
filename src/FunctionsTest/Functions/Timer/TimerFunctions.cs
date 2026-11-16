using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace FunctionsTest.Functions.Timer;

/// <summary>
/// Timer トリガーのパターン集。
/// </summary>
public sealed class TimerFunctions(ILogger<TimerFunctions> logger)
{
#if DEBUG
    // デバッグ時は F5 直後にも 1 回実行して確認しやすくする
    private const bool RunOnStartup = true;
#else
    // Azure ではコールド スタートやスケールアウトのたびに実行されてしまうため無効
    private const bool RunOnStartup = false;
#endif

    /// <summary>
    /// CRON 式をアプリ設定から読む（%TimerSchedule%）。
    /// Debug ビルドでは RunOnStartup により起動直後にも 1 回実行される。
    /// </summary>
    [Function(nameof(TimerFromSetting))]
    public void TimerFromSetting([TimerTrigger("%TimerSchedule%", RunOnStartup = RunOnStartup)] TimerInfo timer)
    {
        logger.LogInformation("TimerFromSetting: 実行 {Now}, 次回 {Next}, 遅延={IsPastDue}",
            DateTimeOffset.Now, timer.ScheduleStatus?.Next, timer.IsPastDue);
    }

    /// <summary>CRON 式を直接書く（毎時 0 分 30 秒）。</summary>
    [Function(nameof(TimerHourly))]
    public void TimerHourly([TimerTrigger("30 0 * * * *")] TimerInfo timer)
    {
        logger.LogInformation("TimerHourly: 実行 {Now}", DateTimeOffset.Now);
    }
}
