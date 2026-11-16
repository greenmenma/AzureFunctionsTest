namespace FunctionsTest.Models;

/// <summary>
/// 各トリガーで共通して使うサンプルのペイロード。
/// </summary>
public sealed record SampleMessage(string Id, string Text, DateTimeOffset CreatedAt)
{
    public static SampleMessage Create(string text) =>
        new(Guid.NewGuid().ToString("N"), text, DateTimeOffset.UtcNow);
}
