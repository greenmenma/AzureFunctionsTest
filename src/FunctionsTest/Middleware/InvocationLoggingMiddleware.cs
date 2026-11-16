using System.Diagnostics;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;

namespace FunctionsTest.Middleware;

/// <summary>
/// すべての関数呼び出しの前後でログを出すミドルウェア。
/// トリガー種別・処理時間・例外を確認できる。
/// </summary>
public sealed class InvocationLoggingMiddleware : IFunctionsWorkerMiddleware
{
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var logger = context.GetLogger<InvocationLoggingMiddleware>();
        var triggerType = context.FunctionDefinition.InputBindings.Values
            .FirstOrDefault(b => b.Type.EndsWith("Trigger", StringComparison.OrdinalIgnoreCase))?.Type ?? "unknown";

        logger.LogInformation("[Middleware] 開始: {FunctionName} (trigger={TriggerType}, invocationId={InvocationId})",
            context.FunctionDefinition.Name, triggerType, context.InvocationId);

        var sw = Stopwatch.StartNew();
        try
        {
            await next(context);
            logger.LogInformation("[Middleware] 終了: {FunctionName} ({ElapsedMs} ms)",
                context.FunctionDefinition.Name, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Middleware] 例外: {FunctionName} ({ElapsedMs} ms)",
                context.FunctionDefinition.Name, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
