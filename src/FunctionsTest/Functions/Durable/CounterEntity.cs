using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Entities;

namespace FunctionsTest.Functions.Durable;

/// <summary>
/// パターン6: 永続エンティティ（クラスベース）。操作名 add / reset / get。
/// </summary>
public sealed class CounterEntity : TaskEntity<int>
{
    public void Add(int? value) => State += value ?? 1;

    public void Reset() => State = 0;

    public int Get() => State;

    [Function(nameof(CounterEntity))]
    public static Task Run([EntityTrigger] TaskEntityDispatcher dispatcher) => dispatcher.DispatchAsync<CounterEntity>();
}
