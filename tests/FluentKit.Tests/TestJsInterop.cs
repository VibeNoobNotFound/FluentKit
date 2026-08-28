using Microsoft.JSInterop;

namespace FluentKit.Tests;

internal sealed class TestJsObjectReference : IJSObjectReference
{
    private readonly Dictionary<string, Exception> _invokeExceptions = new();

    public List<string> Calls { get; } = [];

    public List<(string Identifier, object?[]? Arguments)> Invocations { get; } = [];

    public Dictionary<string, object?> Results { get; } = [];

    public Exception? DisposeException { get; set; }

    public int DisposeCount { get; private set; }

    public void ThrowOnInvoke(string identifier, Exception exception) => _invokeExceptions[identifier] = exception;

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        Calls.Add(identifier);
        Invocations.Add((identifier, args));

        if (_invokeExceptions.TryGetValue(identifier, out var exception))
        {
            return ValueTask.FromException<TValue>(exception);
        }

        if (Results.TryGetValue(identifier, out var result))
        {
            return ValueTask.FromResult((TValue)result!);
        }

        return ValueTask.FromResult(default(TValue)!);
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
        InvokeAsync<TValue>(identifier, args);

    public ValueTask DisposeAsync()
    {
        DisposeCount++;
        Calls.Add("dispose");
        return DisposeException is null
            ? ValueTask.CompletedTask
            : ValueTask.FromException(DisposeException);
    }
}

internal sealed class TestJsRuntime : IJSRuntime
{
    private readonly TestJsObjectReference _module;

    public TestJsRuntime(TestJsObjectReference module)
    {
        _module = module;
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        if (identifier == "import")
        {
            return ValueTask.FromResult((TValue)(object)_module);
        }

        return ValueTask.FromResult(default(TValue)!);
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
        InvokeAsync<TValue>(identifier, args);
}
