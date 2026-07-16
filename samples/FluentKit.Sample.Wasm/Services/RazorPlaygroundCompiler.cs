using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Razor;
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
/// NOTE: this file was written and reasoned about carefully, but could not be built/run in
/// the environment it was authored in (no .NET SDK available there — see the fluentkit skill
/// notes). Please do a first `dotnet build` locally; the riskiest surface is the exact Razor
/// design-time API shape (RazorProjectEngine/CompilerFeatures), which has shifted slightly
/// across `Microsoft.CodeAnalysis.Razor` versions.
/// </summary>
public sealed class RazorPlaygroundCompiler : IRazorPlaygroundCompiler
{
    private readonly HttpClient _http;
    private readonly Dictionary<string, MetadataReference> _referenceCache = new();
    private bool _isWarm;

    public RazorPlaygroundCompiler(HttpClient http)
    {
        _http = http;
    }

    public bool IsWarm => _isWarm;

    public async Task WarmUpAsync()
    {
        if (_isWarm)
        {
            return;
        }

        // Every assembly the running app already needed is sitting right here in the WASM
        // runtime. We just need its raw bytes again so Roslyn can treat it as a reference
        // (loaded assemblies don't expose a real on-disk Location in the browser). Blazor WASM
        // publishes each one as a static file under _framework/, so we re-fetch them over HTTP
        // from the app's own origin — no network access outside the page itself.
        //
        // We ask blazor.boot.json for the *exact* filenames rather than guessing "{name}.dll":
        // asset filenames can be content-fingerprinted (e.g. "System.Private.CoreLib.a1b2c3d4.dll")
        // depending on project settings, and guessing wrong means every single fetch 404s and
        // compilation fails with "System could not be found" (no references at all).
        var fetchedFromBootConfig = await TryWarmUpFromBootConfigAsync();

        if (!fetchedFromBootConfig)
        {
            await WarmUpByGuessingFileNamesAsync();
        }

        _isWarm = true;

        if (_referenceCache.Count == 0)
        {
            // Leave IsWarm = true (no point retrying forever) but the empty cache will surface
            // as a clear pipeline error on the next CompileAsync call rather than a wall of
            // confusing "System could not be found" C# diagnostics.
            Console.Error.WriteLine(
                "[Playground] Warm-up fetched zero assembly references from _framework/. " +
                "Check that <WasmEnableWebcil>false</WasmEnableWebcil> is set and that " +
                "_framework/blazor.boot.json is reachable.");
        }
    }

    /// <summary>Reads _framework/blazor.boot.json and fetches exactly the assembly files it lists.</summary>
    private async Task<bool> TryWarmUpFromBootConfigAsync()
    {
        try
        {
            using var bootStream = await _http.GetStreamAsync("_framework/blazor.boot.json");
            using var bootDoc = await System.Text.Json.JsonDocument.ParseAsync(bootStream);

            if (!bootDoc.RootElement.TryGetProperty("resources", out var resources))
            {
                return false;
            }

            // Assembly filenames live under one or more of these buckets depending on SDK
            // version ("assembly" is the common one; "coreAssembly" shows up when the runtime
            // splits framework assemblies out separately).
            var fileNames = new List<string>();
            foreach (var bucketName in new[] { "assembly", "coreAssembly", "lazyAssembly" })
            {
                if (resources.TryGetProperty(bucketName, out var bucket) &&
                    bucket.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    foreach (var entry in bucket.EnumerateObject())
                    {
                        fileNames.Add(entry.Name);
                    }
                }
            }

            if (fileNames.Count == 0)
            {
                return false;
            }

            var fetches = fileNames.Select(async fileName =>
            {
                if (!fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    // Still webcil (.wasm) despite the csproj setting, or some other asset type
                    // (e.g. a satellite resources dll we don't need) — skip rather than fail.
                    return;
                }

                var cacheKey = fileName[..^".dll".Length];
                if (_referenceCache.ContainsKey(cacheKey))
                {
                    return;
                }

                try
                {
                    var bytes = await _http.GetByteArrayAsync($"_framework/{fileName}");
                    _referenceCache[cacheKey] = MetadataReference.CreateFromImage(bytes);
                }
                catch
                {
                    // One missing/unfetchable asset shouldn't block the rest.
                }
            });

            await Task.WhenAll(fetches);
            return _referenceCache.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Fallback used if blazor.boot.json can't be read: guess "{assembly name}.dll" for every loaded assembly.</summary>
    private async Task WarmUpByGuessingFileNamesAsync()
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.GetName().Name))
            .ToList();

        var fetches = assemblies.Select(async asm =>
        {
            var name = asm.GetName().Name!;
            if (_referenceCache.ContainsKey(name))
            {
                return;
            }

            try
            {
                var bytes = await _http.GetByteArrayAsync($"_framework/{name}.dll");
                _referenceCache[name] = MetadataReference.CreateFromImage(bytes);
            }
            catch
            {
                // Not every loaded assembly is fetchable this way. Missing a reference here
                // only matters if user code actually needs it, in which case Roslyn will
                // surface a normal "type or namespace not found" diagnostic.
            }
        });

        await Task.WhenAll(fetches);
    }

    public async Task<RazorPlaygroundResult> CompileAsync(string razorSource)
    {
        try
        {
            if (!_isWarm)
            {
                await WarmUpAsync();
            }

            if (_referenceCache.Count == 0)
            {
                return RazorPlaygroundResult.Fail(pipelineError:
                    "No metadata references available — the compiler couldn't fetch any of this " +
                    "app's own assemblies from _framework/. Check the browser console for the " +
                    "warm-up warning, and confirm <WasmEnableWebcil>false</WasmEnableWebcil> is " +
                    "set in the Wasm project (webcil-wrapped .wasm assemblies can't be used as " +
                    "Roslyn metadata references directly).");
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

                // Without this, short component tags like <FluentButton> aren't recognized as
                // components at all — the FluentButton namespace is never "in scope", so Razor
                // falls back to treating it as a plain, unknown HTML element (renders literally
                // as <fluentbutton>, no styling, parameters/@onclick become dead HTML attributes).
                // This mirrors the app's own _Imports.razor so playground snippets don't need to
                // repeat these @using lines themselves.
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

            // Razor generates the class named after the file (LiveComponent_xxxx); rename
            // isn't needed since we already picked the file name to match.
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
    /// set (FluentButton, FluentPivot, etc.) resolve as components in playground snippets without
    /// the user having to add @using lines themselves — same experience as any other page.
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
