using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.JSInterop;
using FluentKit.Sample.Shared.Services;

namespace FluentKit.Sample.Wasm.Services;

/// <summary>
/// Compiles a Razor component snippet to a live <see cref="Type"/> entirely in the browser:
/// Razor markup → C# (via the same <see cref="RazorProjectEngine"/> the SDK uses at build time)
/// → assembly bytes (via Roslyn) → <see cref="Assembly.Load(byte[])"/>.
///
/// This is the same general technique used by projects like BlazorRepl / Telerik REPL for
/// Blazor and Thinktecture's WASM Razor template engine: nothing is sent to a server, the
/// .NET runtime already sitting in the page does the compiling.
///
/// For .NET 10, the boot manifest is embedded inside dotnet.js; this compiler fetches that
/// JS file, extracts the fingerprinted assembly filenames, and then downloads each assembly
/// via HttpClient (browser cache handles the rest).
/// </summary>
public sealed class RazorPlaygroundCompiler : IRazorPlaygroundCompiler
{
    private const string CacheModulePath = "./playgroundAssemblyCache.js";

    private readonly HttpClient _http;
    private readonly IJSRuntime _js;
    private readonly Dictionary<string, MetadataReference> _referenceCache = new();
    private bool _isWarm;

    public RazorPlaygroundCompiler(HttpClient http, IJSRuntime js)
    {
        _http = http;
        _js = js;
    }

    public bool IsWarm => _isWarm;

    public async Task WarmUpAsync()
    {
        if (_isWarm)
            return;

        // Try the new manifest‑based approach first (works for .NET 10+).
        var manifestOk = await TryWarmUpFromManifestAsync();
        if (!manifestOk)
        {
            // Fallback: guess filenames (works when no fingerprinting, e.g. local dev).
            await WarmUpByGuessingFileNamesAsync();
        }

        _isWarm = true;

        if (_referenceCache.Count == 0)
        {
            Console.Error.WriteLine(
                "[Playground] Warm-up fetched zero assembly references. Check that " +
                "dotnet.js can be fetched and contains the manifest, or that the " +
                "fallback can locate the assemblies.");
        }
    }

    /// <summary>
    /// Reads the fingerprinted assembly filenames from the dotnet.js boot manifest
    /// (embedded in the file as /*json-start*/ ... /*json-end*/), then downloads each
    /// assembly via HttpClient. The browser's own cache will serve the bytes if they
    /// were already downloaded at startup.
    /// </summary>
    private async Task<bool> TryWarmUpFromManifestAsync()
    {
        try
        {
            await using var module = await _js.InvokeAsync<IJSObjectReference>("import", CacheModulePath);
            var fileNames = await module.InvokeAsync<string[]>("getFrameworkAssemblyManifest");

            if (fileNames == null || fileNames.Length == 0)
                return false;

            var tasks = fileNames.Select(async fileName =>
            {
                if (_referenceCache.ContainsKey(fileName))
                    return;

                try
                {
                    var bytes = await _http.GetByteArrayAsync($"_framework/{fileName}");
                    if (bytes is { Length: > 0 })
                        _referenceCache[fileName] = MetadataReference.CreateFromImage(bytes);
                }
                catch
                {
                    // Ignore – if an assembly can't be fetched, Roslyn will later report
                    // "type not found" errors for types that depend on it.
                }
            });

            await Task.WhenAll(tasks);
            return _referenceCache.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Fallback used when the manifest cannot be read (e.g. dotnet.js is not reachable,
    /// or the host doesn't use fingerprinting). Guesses "{assembly name}.dll" for every
    /// loaded assembly. Works when filenames aren't fingerprinted (e.g. local `dotnet run`),
    /// fails silently otherwise.
    /// </summary>
    private async Task WarmUpByGuessingFileNamesAsync()
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.GetName().Name))
            .ToList();

        var tasks = assemblies.Select(async asm =>
        {
            var name = asm.GetName().Name!;
            if (_referenceCache.ContainsKey(name))
                return;

            try
            {
                var bytes = await _http.GetByteArrayAsync($"_framework/{name}.dll");
                _referenceCache[name] = MetadataReference.CreateFromImage(bytes);
            }
            catch
            {
                // Silently ignore – missing references will surface as compilation errors.
            }
        });

        await Task.WhenAll(tasks);
    }

    public async Task<RazorPlaygroundResult> CompileAsync(string razorSource)
    {
        try
        {
            if (!_isWarm)
                await WarmUpAsync();

            if (_referenceCache.Count == 0)
            {
                return RazorPlaygroundResult.Fail(pipelineError:
                    "No metadata references available — the compiler couldn't read any of this " +
                    "app's own assemblies. Check the browser console for warm-up warnings.");
            }

            var className = $"LiveComponent_{Guid.NewGuid():N}";
            const string rootNamespace = "FluentKit.Playground";

            var projectItem = new InMemoryRazorProjectItem(
                filePath: $"/{className}.razor",
                fileKind: FileKinds.Component,
                content: razorSource);
            var fileSystem = new InMemoryRazorProjectFileSystem(projectItem);

            var references = _referenceCache.Values.ToArray();

            var projectEngine = RazorProjectEngine.Create(RazorConfiguration.Default, fileSystem, builder =>
            {
                builder.SetRootNamespace(rootNamespace);
                CompilerFeatures.Register(builder);
                builder.Features.Add(new CompilationTagHelperFeature());
                builder.Features.Add(new DefaultMetadataReferenceFeature { References = references });

                // This mirrors the app's _Imports.razor so playground snippets don't need
                // to repeat @using lines.
                builder.AddDefaultImports(DefaultImports);
            });

            var codeDocument = projectEngine.Process(projectItem);
            var csharpDocument = codeDocument.GetCSharpDocument();

            var razorErrors = csharpDocument.Diagnostics
                .Where(d => d.Severity == RazorDiagnosticSeverity.Error)
                .Select(d => d.ToString())
                .ToList();

            if (razorErrors.Count > 0)
                return RazorPlaygroundResult.Fail(razorDiagnostics: razorErrors);

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

    /// <summary>
    /// Mirrors samples/FluentKit.Sample.Shared/_Imports.razor so components in that namespace
    /// set (FluentButton, FluentPivot, etc.) resolve as components in playground snippets.
    /// </summary>
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