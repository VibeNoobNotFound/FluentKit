using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Razor;
using FluentKit.Sample.Shared.Services;

namespace Fluentkit.Sample.Maui.Services;

/// <summary>
/// MAUI/BlazorWebView counterpart to <c>FluentKit.Sample.Wasm.Services.RazorPlaygroundCompiler</c>.
///
/// The Wasm version re-fetches its own assemblies over HTTP from "_framework/" because that's
/// the only way to get raw PE bytes back out of a Blazor WebAssembly host. A MAUI app is a
/// normal, native .NET process — there's no "_framework/" HTTP endpoint at all, and this
/// service was previously not registered here, so navigating to the Playground page threw a
/// DI resolution exception ("Cannot provide a value for property 'Compiler'...") the moment the
/// page tried to `@inject IRazorPlaygroundCompiler Compiler`. That's the "Playground crashes on
/// MAUI" bug.
///
/// The fix is also simpler than the Wasm path: loaded assemblies here have a real, readable
/// <see cref="Assembly.Location"/> on disk, so metadata references can be created directly from
/// file, no re-fetching required.
/// </summary>
public sealed class RazorPlaygroundCompiler : IRazorPlaygroundCompiler
{
    private readonly List<MetadataReference> _references = [];
    private bool _isWarm;

    public bool IsWarm => _isWarm;

    public Task WarmUpAsync()
    {
        if (_isWarm)
        {
            return Task.CompletedTask;
        }

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.IsDynamic || string.IsNullOrEmpty(asm.Location) || !File.Exists(asm.Location))
            {
                // Dynamic assemblies and a handful of framework-synthesized ones (no Location)
                // can't be used as metadata references, and just get skipped — same tolerance
                // as the Wasm implementation's per-assembly try/catch.
                continue;
            }

            try
            {
                _references.Add(MetadataReference.CreateFromFile(asm.Location));
            }
            catch
            {
                // Unreadable/locked file, etc. — one bad assembly shouldn't block the rest.
            }
        }

        _isWarm = true;

        if (_references.Count == 0)
        {
            Console.Error.WriteLine(
                "[Playground] Warm-up found zero usable on-disk assemblies via AppDomain.CurrentDomain.GetAssemblies().");
        }

        return Task.CompletedTask;
    }

    public async Task<RazorPlaygroundResult> CompileAsync(string razorSource)
    {
        try
        {
            if (!_isWarm)
            {
                await WarmUpAsync();
            }

            if (_references.Count == 0)
            {
                return RazorPlaygroundResult.Fail(pipelineError:
                    "No metadata references available — the compiler couldn't find any usable " +
                    "on-disk assemblies for this app. Check the debug console for the warm-up warning.");
            }

            var className = $"LiveComponent_{Guid.NewGuid():N}";
            const string rootNamespace = "FluentKit.Playground";

            var projectItem = new InMemoryRazorProjectItem(
                filePath: $"/{className}.razor",
                fileKind: FileKinds.Component,
                content: razorSource);
            var fileSystem = new InMemoryRazorProjectFileSystem(projectItem);

            var references = _references.ToArray();

            var projectEngine = RazorProjectEngine.Create(RazorConfiguration.Default, fileSystem, builder =>
            {
                builder.SetRootNamespace(rootNamespace);
                CompilerFeatures.Register(builder);
                builder.Features.Add(new CompilationTagHelperFeature());
                builder.Features.Add(new DefaultMetadataReferenceFeature { References = references });

                // Mirrors the app's own _Imports.razor so short tags like <FluentButton> resolve
                // as components instead of falling back to plain unknown HTML.
                builder.AddDefaultImports(DefaultImports);
            });

            var codeDocument = projectEngine.Process(projectItem);
            var csharpDocument = codeDocument.GetCSharpDocument();

            var razorErrors = csharpDocument.Diagnostics
                .Where(d => d.Severity == RazorDiagnosticSeverity.Error)
                .Select(d => d.ToString())
                .ToList();

            if (razorErrors.Count > 0)
            {
                return RazorPlaygroundResult.Fail(razorDiagnostics: razorErrors);
            }

            var generatedCode = csharpDocument.GeneratedCode;

            var syntaxTree = CSharpSyntaxTree.ParseText(
                generatedCode,
                new CSharpParseOptions(LanguageVersion.Preview));

            var assemblyName = $"{rootNamespace}.{className}";
            var compilation = CSharpCompilation.Create(
                assemblyName,
                [syntaxTree],
                references,
                new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: OptimizationLevel.Debug,
                    allowUnsafe: false));

            using var peStream = new MemoryStream();
            var emitResult = compilation.Emit(peStream);

            if (!emitResult.Success)
            {
                var csharpErrors = emitResult.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.ToString())
                    .ToList();
                return RazorPlaygroundResult.Fail(csharpDiagnostics: csharpErrors);
            }

            var assembly = Assembly.Load(peStream.ToArray());
            var componentType = assembly.GetType($"{rootNamespace}.{className}")
                ?? assembly.GetTypes().FirstOrDefault(t => typeof(ComponentBase).IsAssignableFrom(t));

            if (componentType is null)
            {
                return RazorPlaygroundResult.Fail(
                    pipelineError: "Compiled successfully, but no component type was found in the output assembly.");
            }

            return RazorPlaygroundResult.Ok(componentType);
        }
        catch (Exception ex)
        {
            return RazorPlaygroundResult.Fail(pipelineError: $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>Mirrors samples/FluentKit.Sample.Shared/_Imports.razor — kept in sync with the Wasm compiler's copy.</summary>
    private const string DefaultImports =
"""
@using System
@using System.Collections.Generic
@using System.Linq
@using System.Net.Http
@using System.Threading.Tasks
@using Microsoft.AspNetCore.Components
@using Microsoft.AspNetCore.Components.Forms
@using Microsoft.AspNetCore.Components.Routing
@using Microsoft.AspNetCore.Components.Web
@using Microsoft.JSInterop
@using FluentKit.Common
@using FluentKit.Theming
@using FluentKit.Overlay
@using FluentKit.Primitives
@using FluentKit.Composite
@using FluentKit.Effects

""";
}
