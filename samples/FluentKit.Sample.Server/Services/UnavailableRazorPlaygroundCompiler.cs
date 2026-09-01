using FluentKit.Sample.Shared.Services;

namespace FluentKit.Sample.Server.Services;

/// <summary>The gallery's in-browser compiler is intentionally unavailable in the Server host.
/// The rest of the shared gallery, including its interactive overlay regressions, remains usable.</summary>
public sealed class UnavailableRazorPlaygroundCompiler : IRazorPlaygroundCompiler
{
    public bool IsWarm => true;

    public Task WarmUpAsync() => Task.CompletedTask;

    public Task<RazorPlaygroundResult> CompileAsync(string razorSource) => Task.FromResult(
        RazorPlaygroundResult.Fail(pipelineError: "The live Razor playground is available in the WebAssembly gallery only."));
}
