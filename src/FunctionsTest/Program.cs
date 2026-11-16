using FunctionsTest.Middleware;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

// HTTP トリガーを ASP.NET Core 統合（HttpRequest / IActionResult）で扱う
builder.ConfigureFunctionsWebApplication();

// isolated worker 特有のミドルウェア機能を確認する（全関数の呼び出し前後でログを出す）
builder.UseMiddleware<InvocationLoggingMiddleware>();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Build().Run();
