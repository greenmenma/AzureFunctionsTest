using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace FunctionsTest.Functions.Durable;

/// <summary>
/// オーケストレーションから呼ばれるアクティビティ群。
/// </summary>
public sealed class Activities(ILogger<Activities> logger)
{
    [Function(nameof(SayHello))]
    public string SayHello([ActivityTrigger] string name)
    {
        logger.LogInformation("SayHello: {Name}", name);
        return $"Hello {name}!";
    }

    [Function(nameof(GetWorkItems))]
    public int[] GetWorkItems([ActivityTrigger] int count) => Enumerable.Range(1, count).ToArray();

    /// <summary>
    /// 二乗を返す。約 30% の確率で一時的な失敗を起こし、リトライポリシーの動作を確認できる。
    /// </summary>
    [Function(nameof(Square))]
    public int Square([ActivityTrigger] int value)
    {
        if (Random.Shared.NextDouble() < 0.3)
        {
            logger.LogWarning("Square: value={Value} で一時的な失敗（リトライされる）", value);
            throw new InvalidOperationException("一時的な失敗");
        }
        return value * value;
    }

    /// <summary>モニター用。4 回目以降に完了を返す。</summary>
    [Function(nameof(CheckStatus))]
    public bool CheckStatus([ActivityTrigger] int attempt)
    {
        logger.LogInformation("CheckStatus: attempt={Attempt}", attempt);
        return attempt >= 4;
    }
}
