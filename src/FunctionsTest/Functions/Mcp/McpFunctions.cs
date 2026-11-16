using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;

namespace FunctionsTest.Functions.Mcp;

/// <summary>
/// MCP（Model Context Protocol）トリガーのパターン集。追加のサービスは不要。
/// MCP サーバーのエンドポイント: http://localhost:7071/runtime/webhooks/mcp（Streamable HTTP）
/// VS Code / Claude などの MCP クライアントから接続して、ツール / リソース / プロンプトを呼び出せる。
/// </summary>
public sealed class McpFunctions(ILogger<McpFunctions> logger)
{
    /// <summary>ツール: 指定したタイムゾーンの現在時刻を返す。</summary>
    [Function(nameof(McpGetTime))]
    public string McpGetTime(
        [McpToolTrigger("get_time", "指定したタイムゾーンの現在時刻を返します。")] ToolInvocationContext context,
        [McpToolProperty("timezone", "IANA または Windows のタイムゾーン ID（省略時は Asia/Tokyo）", false)] string? timezone)
    {
        var tzId = string.IsNullOrWhiteSpace(timezone) ? "Asia/Tokyo" : timezone;
        logger.LogInformation("McpGetTime: timezone={TimeZone}, session={SessionId}", tzId, context.SessionId);

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(tzId, out var tz))
        {
            return $"タイムゾーン '{tzId}' が見つかりません。";
        }
        return TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz).ToString("yyyy-MM-dd HH:mm:ss zzz");
    }

    /// <summary>ツール: 2 つの数値を足す（必須の数値プロパティ）。</summary>
    [Function(nameof(McpAdd))]
    public string McpAdd(
        [McpToolTrigger("add", "2 つの数値を足し算します。")] ToolInvocationContext context,
        [McpToolProperty("a", "1 つ目の数値", true)] double a,
        [McpToolProperty("b", "2 つ目の数値", true)] double b)
    {
        return (a + b).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>リソース: 固定のテキストを返す。</summary>
    [Function(nameof(McpReadmeResource))]
    public string McpReadmeResource(
        [McpResourceTrigger("info://functions-test/about", "about",
            Description = "この検証用関数アプリの説明", MimeType = "text/plain")]
        ResourceInvocationContext context)
    {
        return "Azure Functions のトリガーを網羅的に検証するためのサンプル関数アプリです。";
    }

    /// <summary>プロンプト: 引数を埋め込んだプロンプト テンプレートを返す。</summary>
    [Function(nameof(McpReviewPrompt))]
    public string McpReviewPrompt(
        [McpPromptTrigger("review_trigger", Description = "指定したトリガーの検証観点を洗い出すプロンプト")]
        PromptInvocationContext context,
        [McpPromptArgument("trigger", "トリガー名（例: Service Bus）", true)] string trigger)
    {
        return $"Azure Functions の {trigger} トリガーについて、リトライ・スケール・課金の観点で検証すべき項目を列挙してください。";
    }
}
