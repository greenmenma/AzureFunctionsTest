using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace FunctionsTest.Functions.Timer;

/// <summary>
/// Timer トリガーのパターン集。
/// </summary>
public sealed class TimerFunctions(ILogger<TimerFunctions> logger)
{
    /// <summary>
    /// CRON 式をアプリ設定から読む（%TimerSchedule%）。
    /// RunOnStartup = true なので、F5 直後にも 1 回実行される（本番では false 推奨）。
    /// </summary>
    [Function(nameof(TimerFromSetting))]
    public void TimerFromSetting([TimerTrigger("%TimerSchedule%", RunOnStartup = true)] TimerInfo timer)
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
