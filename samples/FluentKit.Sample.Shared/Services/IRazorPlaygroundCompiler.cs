namespace FluentKit.Sample.Shared.Services;

/// <summary>
/// Compiles a single Razor component (markup + optional @code block) to a live
/// <see cref="Type"/> at runtime, entirely client-side. Backs the Playground page.
/// </summary>
public interface IRazorPlaygroundCompiler
{
    /// <summary>
    /// True once the compiler has warmed up (fetched/cached the metadata references it
    /// needs from the running app's own assemblies). Compiling before this completes
    /// still works, it's just slower on the very first call.
    /// </summary>
    bool IsWarm { get; }

    /// <summary>Pre-fetches metadata references so the first real compile is fast. Safe to call multiple times.</summary>
    Task WarmUpAsync();

    /// <summary>
    /// Compiles <paramref name="razorSource"/> (a full .razor file's contents — markup,
    /// directives, and an optional @code block) into a component <see cref="Type"/> that
    /// can be rendered with Blazor's built-in &lt;DynamicComponent&gt;.
    /// </summary>
    Task<RazorPlaygroundResult> CompileAsync(string razorSource);
}

/// <summary>Outcome of a <see cref="IRazorPlaygroundCompiler.CompileAsync"/> call.</summary>
public sealed class RazorPlaygroundResult
{
    public bool Success { get; init; }

    /// <summary>The compiled component type, ready for &lt;DynamicComponent Type="..."/&gt;. Null on failure.</summary>
    public Type? ComponentType { get; init; }

    /// <summary>Razor-stage diagnostics (bad markup, unclosed tags, directive errors) — shown with source line/column.</summary>
    public IReadOnlyList<string> RazorDiagnostics { get; init; } = [];

    /// <summary>Roslyn-stage diagnostics (C# errors in the generated code / @code block).</summary>
    public IReadOnlyList<string> CSharpDiagnostics { get; init; } = [];

    /// <summary>Any unexpected exception message from the pipeline itself (not a user code error).</summary>
    public string? PipelineError { get; init; }

    public static RazorPlaygroundResult Ok(Type type) => new() { Success = true, ComponentType = type };

    public static RazorPlaygroundResult Fail(
        IReadOnlyList<string>? razorDiagnostics = null,
        IReadOnlyList<string>? csharpDiagnostics = null,
        string? pipelineError = null) => new()
    {
        Success = false,
        RazorDiagnostics = razorDiagnostics ?? [],
        CSharpDiagnostics = csharpDiagnostics ?? [],
        PipelineError = pipelineError
    };
}
