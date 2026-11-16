using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace FunctionsTest.Functions.SignalR;

/// <summary>
/// Azure SignalR Service（サーバーレス モード）のパターン集。
/// ローカルでは SignalR エミュレーター（asrs-emulator）を使う。手順は README を参照。
/// 動作確認用のブラウザー クライアントは GET /api/signalr/client。
/// </summary>
public sealed class SignalRFunctions(ILogger<SignalRFunctions> logger)
{
    private const string Hub = "samplehub";

    /// <summary>クライアントが最初に呼ぶ negotiate。接続先 URL とアクセス トークンを返す。</summary>
    [Function(nameof(SignalRNegotiate))]
    public IActionResult SignalRNegotiate(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "negotiate")] HttpRequest req,
        [SignalRConnectionInfoInput(HubName = Hub, ConnectionStringSetting = "SignalRConnection")] string connectionInfo)
    {
        return new ContentResult { Content = connectionInfo, ContentType = "application/json", StatusCode = 200 };
    }

    /// <summary>SignalR 出力: 接続中の全クライアントへブロードキャストする。</summary>
    [Function(nameof(SignalRBroadcast))]
    public async Task<SignalRBroadcastOutput> SignalRBroadcast(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "signalr/broadcast")] HttpRequest req)
    {
        var text = await new StreamReader(req.Body).ReadToEndAsync();
        return new SignalRBroadcastOutput
        {
            HttpResponse = new AcceptedResult(),
            Message = new SignalRMessageAction("newMessage", [$"[server] {text}"]),
        };
    }

    /// <summary>
    /// SignalR トリガー: クライアントが invoke("sendMessage", text) すると呼ばれる。
    /// 受け取った内容を全員へ返す（エコー チャット）。
    /// エミュレーター / SignalR Service 側にアップストリーム URL の設定が必要。
    /// </summary>
    [Function(nameof(SignalRMessagesTrigger))]
    [SignalROutput(HubName = Hub, ConnectionStringSetting = "SignalRConnection")]
    public SignalRMessageAction SignalRMessagesTrigger(
        [SignalRTrigger(Hub, "messages", "sendMessage", "text", ConnectionStringSetting = "SignalRConnection")]
        SignalRInvocationContext invocation,
        string text)
    {
        logger.LogInformation("SignalRMessagesTrigger: connectionId={ConnectionId}, text={Text}", invocation.ConnectionId, text);
        return new SignalRMessageAction("newMessage", [$"[{invocation.ConnectionId[..6]}] {text}"]);
    }

    /// <summary>接続 / 切断イベントのトリガー。</summary>
    [Function(nameof(SignalRConnectionsTrigger))]
    public void SignalRConnectionsTrigger(
        [SignalRTrigger(Hub, "connections", "connected", ConnectionStringSetting = "SignalRConnection")]
        SignalRInvocationContext invocation)
    {
        logger.LogInformation("SignalRConnectionsTrigger: {Event} connectionId={ConnectionId}", invocation.Event, invocation.ConnectionId);
    }

    /// <summary>動作確認用の最小ブラウザー クライアント。</summary>
    [Function(nameof(SignalRClientPage))]
    public IActionResult SignalRClientPage(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "signalr/client")] HttpRequest req)
    {
        const string html = """
            <!doctype html>
            <html lang="ja"><head><meta charset="utf-8"><title>SignalR テスト</title>
            <script src="https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/8.0.7/signalr.min.js"></script></head>
            <body style="font-family:sans-serif">
              <h1>SignalR テスト クライアント</h1>
              <p id="status">接続中…</p>
              <input id="text" placeholder="メッセージ"> <button id="send">送信（SignalR トリガー経由）</button>
              <ul id="log"></ul>
              <script>
                const log = t => { const li = document.createElement('li'); li.textContent = t; document.getElementById('log').prepend(li); };
                const conn = new signalR.HubConnectionBuilder().withUrl('/api').withAutomaticReconnect().build();
                conn.on('newMessage', m => log(m));
                conn.start().then(() => document.getElementById('status').textContent = '接続しました')
                            .catch(e => document.getElementById('status').textContent = '接続失敗: ' + e);
                document.getElementById('send').onclick = () => conn.invoke('sendMessage', document.getElementById('text').value).catch(e => log('送信失敗: ' + e));
              </script>
            </body></html>
            """;
        return new ContentResult { Content = html, ContentType = "text/html; charset=utf-8", StatusCode = 200 };
    }
}

/// <summary>HTTP 応答 + SignalR 出力。</summary>
public sealed class SignalRBroadcastOutput
{
    [HttpResult]
    public required IActionResult HttpResponse { get; init; }

    [SignalROutput(HubName = "samplehub", ConnectionStringSetting = "SignalRConnection")]
    public SignalRMessageAction? Message { get; init; }
}
