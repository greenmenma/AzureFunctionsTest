using Azure.Storage.Queues.Models;
using FunctionsTest.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace FunctionsTest.Functions.Storage;

/// <summary>
/// Storage Queue トリガーのパターン集（Azurite で動作）。
/// </summary>
public sealed class QueueFunctions(ILogger<QueueFunctions> logger)
{
    /// <summary>
    /// POCO へ自動でデシリアライズして受け取る。
    /// 戻り値を Blob 出力にして、処理結果を results コンテナーへ保存する（Queue → Blob の連鎖）。
    /// </summary>
    [Function(nameof(QueueTrigger))]
    [BlobOutput("results/{rand-guid}.json", Connection = "StorageConnection")]
    public SampleMessage QueueTrigger(
        [QueueTrigger("sample-queue", Connection = "StorageConnection")] SampleMessage message,
        FunctionContext context)
    {
        // トリガーのメタデータ（DequeueCount など）はバインディングデータから参照できる
        context.BindingContext.BindingData.TryGetValue("DequeueCount", out var dequeueCount);
        logger.LogInformation("QueueTrigger: id={Id}, text={Text}, dequeueCount={DequeueCount}",
            message.Id, message.Text, dequeueCount);
        return message with { Text = $"[processed] {message.Text}" };
    }

    /// <summary>
    /// SDK 型（QueueMessage）で受け取る。
    /// 本文に "fail" が含まれると例外を投げる → host.json の maxDequeueCount(3) 回失敗後に
    /// "sample-queue-sdk-poison" キューへ移ることを確認できる。
    /// </summary>
    [Function(nameof(QueueTriggerSdkType))]
    public void QueueTriggerSdkType(
        [QueueTrigger("sample-queue-sdk", Connection = "StorageConnection")] QueueMessage message)
    {
        var body = message.Body.ToString();
        logger.LogInformation("QueueTriggerSdkType: messageId={MessageId}, dequeueCount={DequeueCount}, body={Body}",
            message.MessageId, message.DequeueCount, body);

        if (body.Contains("fail", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("意図的な失敗（poison キュー確認用）");
        }
    }

    /// <summary>poison キューを監視して、移ってきたメッセージを記録する。</summary>
    [Function(nameof(QueuePoisonTrigger))]
    public void QueuePoisonTrigger(
        [QueueTrigger("sample-queue-sdk-poison", Connection = "StorageConnection")] string message)
    {
        logger.LogWarning("QueuePoisonTrigger: poison に移ったメッセージ body={Body}", message);
    }
}
