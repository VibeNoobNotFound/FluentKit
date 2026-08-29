using System.Reflection;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using Microsoft.AspNetCore.Components;

return await ApiReferenceGenerator.RunAsync(args);

internal static class ApiReferenceGenerator
{
    // Keep generated JSON stable so it can be compared in CI and embedded byte-for-byte in
    // the package-local agent contract.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task<int> RunAsync(string[] args)
    {
        // The generator deliberately has no implicit repository paths. CI and release scripts
        // pass every input/output path explicitly, which also makes local regeneration safe.
        var options = ParseOptions(args);
        if (options.ContainsKey("help"))
        {
            PrintUsage();
            return 0;
        }

        if (options.ContainsKey("self-test"))
        {
            return SelfTest();
        }

        if (options.TryGetValue("verify-package", out var packagePath))
        {
            return VerifyPackage(packagePath, options);
        }

        if (!options.TryGetValue("assembly", out var assemblyPath) ||
            !options.TryGetValue("manifest", out var manifestPath) ||
            !options.TryGetValue("json", out var jsonPath) ||
            !options.TryGetValue("markdown", out var markdownPath))
        {
            Console.Error.WriteLine("--assembly, --manifest, --json, and --markdown are required.");
            PrintUsage();
            return 2;
        }

        var xmlPath = options.GetValueOrDefault("xml") ?? Path.ChangeExtension(assemblyPath, ".xml");
        if (!File.Exists(assemblyPath))
        {
            Console.Error.WriteLine($"Assembly not found: {assemblyPath}");
            return 2;
        }

        // Reflection describes the shipped binary; XML documentation supplies the human text.
        // This prevents the API reference from becoming a second, manually maintained API list.
        var model = BuildModel(assemblyPath, xmlPath, manifestPath);
        if (options.TryGetValue("summary-baseline", out var baselineOutput) && !options.ContainsKey("check-summaries"))
        {
            // The initial baseline records existing omissions. Future omissions are rejected by
            // --check-summaries, so adding a new public parameter requires an XML summary.
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(baselineOutput))!);
            await File.WriteAllTextAsync(baselineOutput, JsonSerializer.Serialize(model.MissingSummaries, JsonOptions) + Environment.NewLine, Encoding.UTF8);
        }
        if (options.TryGetValue("summary-baseline", out var summaryBaselinePath) && options.ContainsKey("check-summaries"))
        {
            var allowed = File.Exists(summaryBaselinePath)
                ? JsonSerializer.Deserialize<string[]>(File.ReadAllText(summaryBaselinePath), JsonOptions)?.ToHashSet(StringComparer.Ordinal)
                  ?? new HashSet<string>(StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);
            // Existing undocumented members are tolerated through the checked-in baseline;
            // only newly exposed undocumented members fail the build.
            var undocumented = model.MissingSummaries.Except(allowed, StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            if (undocumented.Length > 0)
            {
                Console.Error.WriteLine("New public API is missing XML summaries:");
                foreach (var item in undocumented) Console.Error.WriteLine($"  {item}");
                return 1;
            }
        }
        var json = JsonSerializer.Serialize(model, JsonOptions) + Environment.NewLine;
        var markdown = RenderMarkdown(model);
        var contractOutput = options.GetValueOrDefault("contract-output");
        var contract = contractOutput is null
            ? null
            : CreateAgentContract(
                model,
                options.GetValueOrDefault("package-id") ?? "FluentKit.Blazor",
                options.GetValueOrDefault("package-version") ?? throw new InvalidOperationException("--package-version is required with --contract-output."),
                options.GetValueOrDefault("skill-source") ?? throw new InvalidOperationException("--skill-source is required with --contract-output."),
                options.GetValueOrDefault("references-source") ?? throw new InvalidOperationException("--references-source is required with --contract-output."),
                json,
                markdown);

        if (options.ContainsKey("verify"))
        {
            // Verification never writes tracked files. It is used by PR/release CI to catch
            // developers who changed the assembly without regenerating the checked-in output.
            var ok = CompareFile(jsonPath, json) & CompareFile(markdownPath, markdown);
            if (contract is not null)
            {
                ok &= VerifyAgentContract(contract, contractOutput!);
            }
            return ok ? 0 : 1;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(jsonPath))!);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(markdownPath))!);
        await File.WriteAllTextAsync(jsonPath, json, Encoding.UTF8);
        await File.WriteAllTextAsync(markdownPath, markdown, Encoding.UTF8);
        if (contract is not null)
        {
            WriteAgentContract(contract, contractOutput!);
        }
        return 0;
    }

    private static AgentContract CreateAgentContract(
        ReferenceModel model,
        string packageId,
        string packageVersion,
        string skillSource,
        string referencesSource,
        string json,
        string markdown)
    {
        if (!File.Exists(skillSource))
            throw new InvalidOperationException($"Agent skill source not found: {skillSource}");
        if (!Directory.Exists(referencesSource))
            throw new InvalidOperationException($"Agent references directory not found: {referencesSource}");

        ValidatePackageVersion(model.Version, packageVersion);

        var referenceNames = new[]
        {
            "setup",
            "component-selection",
            "theming-and-tokens",
            "overlays",
            "icons",
            "troubleshooting"
        };
        var referenceSources = referenceNames.ToDictionary(
            name => name,
            name => Path.Combine(referencesSource, name + ".md"),
            StringComparer.Ordinal);
        foreach (var source in referenceSources.Values)
        {
            if (!File.Exists(source))
                throw new InvalidOperationException($"Agent reference source not found: {source}");
        }
        var references = referenceSources.ToDictionary(
            x => x.Key,
            x => "references/" + Path.GetFileName(x.Value),
            StringComparer.Ordinal);
        var files = new[] { "SKILL.md", "api.json", "api.md" }
            .Concat(references.Values)
            .ToDictionary(
                path => path,
                path => path switch
                {
                    "SKILL.md" => Sha256(skillSource),
                    "api.json" => Sha256(Utf8Bytes(json)),
                    "api.md" => Sha256(Utf8Bytes(markdown)),
                    _ => Sha256(referenceSources.Single(x => references[x.Key] == path).Value)
                },
                StringComparer.Ordinal);

        return new AgentContract(
            new AgentContractManifest(
                1,
                packageId,
                packageVersion,
                model.Assembly,
                "SKILL.md",
                "api.json",
                "api.md",
                references,
                "https://vibenoobnotfound.github.io/FluentKit/",
                "https://github.com/VibeNoobNotFound/FluentKit/tree/main/docs/integration",
                files),
            json,
            markdown,
            skillSource,
            referenceSources);
    }

    private static void WriteAgentContract(AgentContract contract, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(outputDirectory, "api.json"), contract.Json, Encoding.UTF8);
        File.WriteAllText(Path.Combine(outputDirectory, "api.md"), contract.Markdown, Encoding.UTF8);
        File.Copy(contract.SkillSource, Path.Combine(outputDirectory, "SKILL.md"), true);
        var referenceDirectory = Path.Combine(outputDirectory, "references");
        Directory.CreateDirectory(referenceDirectory);
        foreach (var source in contract.ReferenceSources.Values)
        {
            File.Copy(source, Path.Combine(referenceDirectory, Path.GetFileName(source)), true);
        }
        var manifestPath = Path.Combine(outputDirectory, "manifest.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(contract.Manifest, JsonOptions) + Environment.NewLine, Encoding.UTF8);
    }

    private static bool VerifyAgentContract(AgentContract contract, string outputDirectory)
    {
        var ok = true;
        ok &= CompareFile(Path.Combine(outputDirectory, "api.json"), contract.Json);
        ok &= CompareFile(Path.Combine(outputDirectory, "api.md"), contract.Markdown);
        ok &= CompareFile(Path.Combine(outputDirectory, "SKILL.md"), File.ReadAllText(contract.SkillSource));
        foreach (var source in contract.ReferenceSources)
        {
            ok &= CompareFile(
                Path.Combine(outputDirectory, contract.Manifest.References[source.Key].Replace('/', Path.DirectorySeparatorChar)),
                File.ReadAllText(source.Value));
        }
        var manifestPath = Path.Combine(outputDirectory, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            Console.Error.WriteLine($"Agent contract manifest is missing: {manifestPath}");
            return false;
        }

        var expected = JsonSerializer.Serialize(contract.Manifest, JsonOptions) + Environment.NewLine;
        ok &= CompareFile(manifestPath, expected);
        foreach (var path in contract.Manifest.Files.Keys)
        {
            var fullPath = Path.Combine(outputDirectory, path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath))
            {
                Console.Error.WriteLine($"Agent contract file is missing: {fullPath}");
                ok = false;
                continue;
            }
            if (!string.Equals(Sha256(fullPath), contract.Manifest.Files[path], StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine($"Agent contract file has the wrong hash: {fullPath}");
                ok = false;
            }
        }
        return ok;
    }

    private static int VerifyPackage(string packagePath, IReadOnlyDictionary<string, string> options)
    {
        if (!File.Exists(packagePath))
        {
            Console.Error.WriteLine($"NuGet package not found: {packagePath}");
            return 2;
        }

        try
        {
            using var archive = ZipFile.OpenRead(packagePath);
            var entries = archive.Entries.ToDictionary(x => x.FullName, StringComparer.Ordinal);
            var required = new[]
            {
                "fluentkit/agent/v1/manifest.json",
                "fluentkit/agent/v1/SKILL.md",
                "fluentkit/agent/v1/api.json",
                "fluentkit/agent/v1/api.md",
                "buildTransitive/FluentKit.Blazor.props",
                "buildTransitive/FluentKit.Blazor.Agent.props"
            };
            var ok = true;
            foreach (var path in required)
            {
                if (!entries.ContainsKey(path))
                {
                    Console.Error.WriteLine($"NuGet package is missing: {path}");
                    ok = false;
                }
            }
            if (!ok) return 1;

            var manifest = ReadJson<AgentContractManifest>(entries["fluentkit/agent/v1/manifest.json"]);
            if (manifest.SchemaVersion != 1 || string.IsNullOrWhiteSpace(manifest.PackageId) ||
                string.IsNullOrWhiteSpace(manifest.PackageVersion) || string.IsNullOrWhiteSpace(manifest.AssemblyName) ||
                !string.Equals(manifest.PackageId, "FluentKit.Blazor", StringComparison.Ordinal) ||
                !string.Equals(manifest.Skill, "SKILL.md", StringComparison.Ordinal) ||
                !string.Equals(manifest.ApiJson, "api.json", StringComparison.Ordinal) ||
                !string.Equals(manifest.ApiMarkdown, "api.md", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(manifest.SampleBaseUrl) ||
                string.IsNullOrWhiteSpace(manifest.DocumentationBaseUrl) ||
                manifest.Files is null || manifest.Files.Count == 0 || manifest.References is null)
            {
                Console.Error.WriteLine("Agent contract manifest is invalid.");
                return 1;
            }
            if (manifest.Files.Keys.Any(path => !IsSafeRelativePath(path)) ||
                manifest.References.Values.Any(path => !IsSafeRelativePath(path) || !path.StartsWith("references/", StringComparison.Ordinal)))
            {
                Console.Error.WriteLine("Agent contract manifest contains an unsafe relative path.");
                return 1;
            }

            using var nuspecStream = entries.Values.FirstOrDefault(x => x.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase))?.Open();
            if (nuspecStream is null)
            {
                Console.Error.WriteLine("NuGet package does not contain a nuspec.");
                return 1;
            }
            var nuspec = XDocument.Load(nuspecStream);
            var metadata = nuspec.Descendants().FirstOrDefault(x => x.Name.LocalName == "metadata");
            var packageId = metadata?.Elements().FirstOrDefault(x => x.Name.LocalName == "id")?.Value;
            var packageVersion = metadata?.Elements().FirstOrDefault(x => x.Name.LocalName == "version")?.Value;
            if (!string.Equals(packageId, manifest.PackageId, StringComparison.Ordinal) ||
                !string.Equals(packageVersion, manifest.PackageVersion, StringComparison.Ordinal))
            {
                Console.Error.WriteLine("Agent contract package identity does not match the nuspec.");
                ok = false;
            }

            var apiJsonPath = "fluentkit/agent/v1/" + manifest.ApiJson;
            var apiMarkdownPath = "fluentkit/agent/v1/" + manifest.ApiMarkdown;
            if (!entries.ContainsKey(apiJsonPath) || !entries.ContainsKey(apiMarkdownPath))
            {
                Console.Error.WriteLine("Agent contract API paths are missing from the package.");
                return 1;
            }
            var model = ReadJson<ReferenceModel>(entries[apiJsonPath]);
            if (!string.Equals(model.Assembly, manifest.AssemblyName, StringComparison.Ordinal) ||
                !string.Equals(model.Version, StripPrerelease(manifest.PackageVersion), StringComparison.Ordinal))
            {
                Console.Error.WriteLine("Agent contract API identity does not match the manifest.");
                ok = false;
            }
            var markdown = ReadEntry(entries[apiMarkdownPath]);
            if (!string.Equals(markdown, RenderMarkdown(model), StringComparison.Ordinal))
            {
                Console.Error.WriteLine("Packaged api.md is not generated from packaged api.json.");
                ok = false;
            }

            if (options.TryGetValue("assembly", out var builtAssemblyPath))
            {
                if (!options.TryGetValue("manifest", out var builtManifestPath))
                    throw new InvalidOperationException("--manifest is required when --assembly is supplied with --verify-package.");
                var builtXmlPath = options.GetValueOrDefault("xml") ?? Path.ChangeExtension(builtAssemblyPath, ".xml");
                var builtModel = BuildModel(builtAssemblyPath, builtXmlPath, builtManifestPath);
                var builtJson = JsonSerializer.Serialize(builtModel, JsonOptions) + Environment.NewLine;
                var builtMarkdown = RenderMarkdown(builtModel);
                if (!string.Equals(ReadEntry(entries[apiJsonPath]), builtJson, StringComparison.Ordinal))
                {
                    Console.Error.WriteLine("Packaged api.json differs from the freshly generated built-assembly API.");
                    ok = false;
                }
                if (!string.Equals(markdown, builtMarkdown, StringComparison.Ordinal))
                {
                    Console.Error.WriteLine("Packaged api.md differs from the freshly generated built-assembly API.");
                    ok = false;
                }
            }

            foreach (var file in manifest.Files)
            {
                var entryPath = "fluentkit/agent/v1/" + file.Key;
                if (!entries.TryGetValue(entryPath, out var entry))
                {
                    Console.Error.WriteLine($"Manifest file is missing from package: {file.Key}");
                    ok = false;
                    continue;
                }
                using var stream = entry.Open();
                if (!string.Equals(Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant(), file.Value, StringComparison.OrdinalIgnoreCase))
                {
                    Console.Error.WriteLine($"Manifest hash mismatch: {file.Key}");
                    ok = false;
                }
            }

            var props = ReadEntry(entries["buildTransitive/FluentKit.Blazor.Agent.props"]);
            var propsDocument = XDocument.Parse(props);
            foreach (var property in new[] { "FluentKitAgentContractRoot", "FluentKitAgentManifestPath", "FluentKitAgentSkillPath" })
            {
                if (!propsDocument.Descendants().Any(x => x.Name.LocalName == property))
                {
                    Console.Error.WriteLine($"Agent props does not expose {property}.");
                    ok = false;
                }
            }
            var wrapperProps = ReadEntry(entries["buildTransitive/FluentKit.Blazor.props"]);
            _ = XDocument.Parse(wrapperProps);
            if (!wrapperProps.Contains("FluentKit.Blazor.Agent.props", StringComparison.Ordinal) ||
                !wrapperProps.Contains("../buildMultiTargeting/FluentKit.Blazor.props", StringComparison.Ordinal))
            {
                Console.Error.WriteLine("Conventional buildTransitive props do not import the static-assets and agent props.");
                ok = false;
            }
            return ok ? 0 : 1;
        }
        catch (Exception ex) when (ex is InvalidDataException or JsonException or InvalidOperationException or System.Xml.XmlException)
        {
            Console.Error.WriteLine($"NuGet package contract verification failed: {ex.Message}");
            return 1;
        }
    }

    private static T ReadJson<T>(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        return JsonSerializer.Deserialize<T>(stream, JsonOptions) ?? throw new InvalidDataException($"Empty JSON entry: {entry.FullName}");
    }

    private static string ReadEntry(ZipArchiveEntry entry)
    {
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string Sha256(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static byte[] Utf8Bytes(string text) => Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(text)).ToArray();

    private static void ValidatePackageVersion(string assemblyVersion, string packageVersion)
    {
        var assemblyCore = StripPrerelease(assemblyVersion);
        var packageCore = StripPrerelease(packageVersion);
        if (!string.Equals(assemblyCore, packageCore, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Package version {packageVersion} does not match assembly version {assemblyVersion}.");
        }
    }

    private static string StripPrerelease(string version) => version.Split('-', '+')[0];

    private static bool IsSafeRelativePath(string path) =>
        !string.IsNullOrWhiteSpace(path) &&
        !path.StartsWith("/", StringComparison.Ordinal) &&
        !path.Contains("\\", StringComparison.Ordinal) &&
        !path.Split('/').Any(segment => segment is "" or "." or "..");

    private static ReferenceModel BuildModel(string assemblyPath, string xmlPath, string manifestPath)
    {
        // Load the built assembly instead of source files: Razor-generated component classes,
        // inherited parameters, generic types, and compiler output are then represented exactly
        // as they appear at runtime.
        var assembly = Assembly.LoadFrom(Path.GetFullPath(assemblyPath));
        var xml = LoadXml(xmlPath);
        var manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(manifestPath), JsonOptions)
                       ?? throw new InvalidOperationException("The documentation manifest is empty.");

        // Restrict the inventory to FluentKit's namespace. Dependencies such as Components.Web
        // are implementation prerequisites, not part of FluentKit's published API reference.
        var publicTypes = assembly.GetExportedTypes()
            .Where(t => t.Namespace?.StartsWith("FluentKit", StringComparison.Ordinal) == true)
            .Where(t => !t.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false))
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .ToArray();

        // Components are documented separately because their Blazor parameters and sample route
        // are more useful for application developers than a normal CLR type/member listing.
        var components = publicTypes
            .Where(IsComponent)
            .Select(t => DescribeComponent(t, xml, manifest.Components))
            .OrderBy(c => c.Name, StringComparer.Ordinal)
            .ToArray();

        var componentNames = components.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
        var manifestNames = manifest.Components.Keys.ToHashSet(StringComparer.Ordinal);
        var missing = componentNames.Except(manifestNames, StringComparer.Ordinal).OrderBy(x => x).ToArray();
        var extra = manifestNames.Except(componentNames, StringComparer.Ordinal).OrderBy(x => x).ToArray();
        // The manifest is intentionally checked in: it carries category/sample intent that
        // cannot be inferred reliably from namespaces alone.
        if (missing.Length > 0 || extra.Length > 0)
        {
            throw new InvalidOperationException(
                $"Component documentation manifest mismatch. Missing: {string.Join(", ", missing)}. " +
                $"Unknown: {string.Join(", ", extra)}.");
        }

        var types = publicTypes
            .Where(t => !IsComponent(t))
            .Select(t => DescribeType(t, xml))
            .ToArray();

        // Track missing summaries by XML documentation key. This gives the baseline a stable
        // identity even when generated Markdown formatting changes.
        var missingSummaries = publicTypes
            .SelectMany(type =>
            {
                var missing = new List<string>();
                if (xml.Summary(TypeKey(type)) is null) missing.Add(TypeKey(type));
                if (IsComponent(type))
                {
                    foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                 .Where(p => p.GetCustomAttribute<ParameterAttribute>(true) is not null ||
                                             p.GetCustomAttribute<CascadingParameterAttribute>(true) is not null)
                                 .Where(p => p.Name != "AdditionalAttributes" && p.Name != "ChildContent"))
                    {
                        if (xml.Summary(PropertyKey(property)) is null) missing.Add(PropertyKey(property));
                    }
                }
                return missing;
            })
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        return new ReferenceModel(
            assembly.GetName().Name ?? "FluentKit",
            assembly.GetName().Version?.ToString(3) ?? "0.0.0",
            components,
            types,
            missingSummaries);
    }

    private static ComponentReference DescribeComponent(Type type, XmlDocs xml, IReadOnlyDictionary<string, ManifestEntry> manifest)
    {
        // Generic arity is a CLR implementation detail; Razor uses the friendly type
        // name (for example FluentComboBox<TValue> becomes FluentComboBox).
        var name = type.Name.Split('`')[0];
        var entry = manifest.TryGetValue(name, out var manifestEntry)
            ? manifestEntry
            : new ManifestEntry();
        // Parameter attributes are the authoritative Blazor component contract. Matching a
        // sibling *Changed property marks a normal two-way bindable parameter.
        var parameters = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<ParameterAttribute>(true) is not null ||
                        p.GetCustomAttribute<CascadingParameterAttribute>(true) is not null)
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .Select(p =>
            {
                var changed = type.GetProperty(p.Name + "Changed", BindingFlags.Public | BindingFlags.Instance);
                var cascading = p.GetCustomAttribute<CascadingParameterAttribute>(true) is not null;
                return new ParameterReference(
                    p.Name,
                    FriendlyTypeName(p.PropertyType),
                    cascading,
                    p.PropertyType == typeof(RenderFragment) || p.PropertyType == typeof(RenderFragment<>),
                    changed is not null,
                    xml.Summary(PropertyKey(p)),
                    p.CanWrite);
            })
            .ToArray();

        return new ComponentReference(
            name,
            type.FullName ?? type.Name,
            entry.Category,
            entry.Sample,
            type.GetGenericArguments().Select(x => x.Name).ToArray(),
            xml.Summary(TypeKey(type)),
            parameters);
    }

    private static TypeReference DescribeType(Type type, XmlDocs xml)
    {
        // Enums retain their values; interfaces/services and helper classes retain declared
        // public properties/methods so agents can discover service contracts from api.json.
        var values = type.IsEnum
            ? Enum.GetNames(type).OrderBy(x => x, StringComparer.Ordinal).ToArray()
            : Array.Empty<string>();
        var members = type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(m => m is MethodInfo { IsSpecialName: false } || m is PropertyInfo)
            .Where(m => !m.Name.Contains("<", StringComparison.Ordinal))
            .OrderBy(m => m.Name, StringComparer.Ordinal)
            .Select(m => m switch
            {
                MethodInfo method => new MemberReference(method.Name, "method", FriendlyTypeName(method.ReturnType), xml.Summary(MethodKey(method))),
                PropertyInfo property => new MemberReference(property.Name, "property", FriendlyTypeName(property.PropertyType), xml.Summary(PropertyKey(property))),
                _ => throw new InvalidOperationException()
            })
            .ToArray();
        return new TypeReference(
            type.Name,
            type.FullName ?? type.Name,
            type.IsEnum ? "enum" : type.IsInterface ? "interface" : type.IsValueType ? "struct" : "class",
            type.GetGenericArguments().Select(x => x.Name).ToArray(),
            values,
            xml.Summary(TypeKey(type)),
            members);
    }

    private static bool IsComponent(Type type) =>
        // IComponent catches Razor components while excluding public enums, services, records,
        // and other supporting types that belong in the public-types section.
        type.IsClass && !type.IsAbstract && typeof(IComponent).IsAssignableFrom(type);

    private static string RenderMarkdown(ReferenceModel model)
    {
        // Markdown is a human-facing projection of the same model serialized to api.json. Keep
        // no API facts here that are absent from the machine-readable model.
        var b = new StringBuilder();
        b.AppendLine("# FluentKit API reference");
        b.AppendLine();
        b.AppendLine($"> Generated from `{model.Assembly}` version `{model.Version}`. Do not edit this file by hand.");
        b.AppendLine();
        b.AppendLine("## Components");
        b.AppendLine();
        foreach (var component in model.Components)
        {
            b.AppendLine($"### `{component.Name}`");
            b.AppendLine();
            b.AppendLine($"Category: **{component.Category}** · Sample: `{component.Sample}`");
            if (!string.IsNullOrWhiteSpace(component.Summary)) b.AppendLine(component.Summary);
            b.AppendLine();
            if (component.GenericParameters.Length > 0)
                b.AppendLine($"Generic parameters: `{string.Join("`, `", component.GenericParameters)}`");
            b.AppendLine();
            b.AppendLine("| Parameter | Type | Cascading | RenderFragment | Bindable | Description |");
            b.AppendLine("| --- | --- | --- | --- | --- | --- |");
            foreach (var parameter in component.Parameters)
            {
                var summary = (parameter.Summary ?? "").Replace("|", "\\|", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
                b.AppendLine($"| `{parameter.Name}` | `{parameter.Type}` | {YesNo(parameter.Cascading)} | {YesNo(parameter.RenderFragment)} | {YesNo(parameter.Bindable)} | {summary} |");
            }
            b.AppendLine();
        }

        b.AppendLine("## Public types");
        b.AppendLine();
        b.AppendLine("| Type | Kind | Members | Description |");
        b.AppendLine("| --- | --- | --- | --- |");
        foreach (var type in model.Types)
        {
            var summary = (type.Summary ?? "").Replace("|", "\\|", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
            var values = string.Join(", ", type.Values.Select(v => $"`{v}`"));
            var members = string.Join(", ", type.Members.Select(m => $"`{m.Name}`"));
            b.AppendLine($"| `{type.Name}` | {type.Kind} | {values}{(values.Length > 0 && members.Length > 0 ? "; " : "")}{members} | {summary} |");
        }
        return b.ToString();
    }

    private static string YesNo(bool value) => value ? "yes" : "no";

    private static bool CompareFile(string path, string expected)
    {
        // Compare exact bytes/text rather than timestamps so generated output is deterministic
        // across developer machines and CI runners.
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"Generated file is missing: {path}");
            return false;
        }
        var actual = File.ReadAllText(path);
        if (actual == expected) return true;
        Console.Error.WriteLine($"Generated file is stale: {path}");
        return false;
    }

    private static XmlDocs LoadXml(string path)
    {
        // C# XML documentation uses keys such as T:Type and P:Type.Property. The lookup wrapper
        // keeps XML parsing details out of the reflection code above.
        if (!File.Exists(path)) return new XmlDocs(new Dictionary<string, string>());
        var document = XDocument.Load(path);
        var members = document.Descendants("member")
            .Where(x => x.Attribute("name") is not null)
            .ToDictionary(x => x.Attribute("name")!.Value, x => Normalize(x.Element("summary")?.Value), StringComparer.Ordinal);
        return new XmlDocs(members);
    }

    private static string Normalize(string? text) =>
        string.Join(" ", (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string TypeKey(Type type) => "T:" + (type.FullName ?? type.Name).Replace('+', '.');
    private static string PropertyKey(PropertyInfo property) => "P:" + (property.DeclaringType?.FullName ?? property.ReflectedType?.FullName ?? "").Replace('+', '.') + "." + property.Name;
    private static string MethodKey(MethodInfo method) => "M:" + (method.DeclaringType?.FullName ?? method.ReflectedType?.FullName ?? "").Replace('+', '.') + "." + method.Name;

    private static string FriendlyTypeName(Type type)
    {
        // Reflection names generic types with backtick arity; this converts them into the syntax
        // a developer will write in Razor/C# documentation.
        if (type.IsArray) return FriendlyTypeName(type.GetElementType()!) + "[]";
        if (type.IsGenericType)
        {
            var name = type.Name[..type.Name.IndexOf('`')];
            return name + "<" + string.Join(", ", type.GetGenericArguments().Select(FriendlyTypeName)) + ">";
        }
        return type.FullName?.Replace("System.", "", StringComparison.Ordinal) ?? type.Name;
    }

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        // Flags are intentionally minimal: this is a CI maintenance executable, not a general
        // command-line framework. A flag without a value is represented as "true".
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal)) continue;
            var key = args[i][2..];
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal)) result[key] = args[++i];
            else result[key] = "true";
        }
        return result;
    }

    private static int SelfTest()
    {
        // The fixture checks the reflection shapes most likely to regress when the generator is
        // edited: generic components, bindable callbacks, child content, cascading parameters,
        // service interfaces, and enums.
        var properties = typeof(FixtureComponent<>).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<ParameterAttribute>() is not null || p.GetCustomAttribute<CascadingParameterAttribute>() is not null)
            .ToArray();
        if (properties.Length != 4 || properties.All(p => p.Name != "ValueChanged") || properties.All(p => p.Name != "ChildContent") ||
            !typeof(FixtureService).IsInterface || Enum.GetNames<FixtureKind>().Length != 2)
        {
            Console.Error.WriteLine("Generator fixture self-test failed.");
            return 1;
        }
        return 0;
    }

    private static void PrintUsage() => Console.WriteLine("Usage: --assembly PATH --manifest PATH --json PATH --markdown PATH [--xml PATH] [--verify] [--contract-output PATH --package-id ID --package-version VERSION --skill-source PATH --references-source PATH] | --verify-package PATH [--assembly PATH --xml PATH --manifest PATH] | --self-test");

    private sealed class XmlDocs(IReadOnlyDictionary<string, string> members)
    {
        public string? Summary(string key) => members.TryGetValue(key, out var value) && value.Length > 0 ? value : null;
    }

    private sealed class Manifest
    {
        public Dictionary<string, ManifestEntry> Components { get; set; } = new(StringComparer.Ordinal);
    }

    private sealed class ManifestEntry
    {
        public string Category { get; set; } = "Uncategorized";
        public string Sample { get; set; } = "/";
    }

    private sealed record ReferenceModel(string Assembly, string Version, ComponentReference[] Components, TypeReference[] Types, string[] MissingSummaries);
    private sealed record AgentContract(
        AgentContractManifest Manifest,
        string Json,
        string Markdown,
        string SkillSource,
        Dictionary<string, string> ReferenceSources);
    private sealed record AgentContractManifest(
        int SchemaVersion,
        string PackageId,
        string PackageVersion,
        string AssemblyName,
        string Skill,
        string ApiJson,
        string ApiMarkdown,
        Dictionary<string, string> References,
        string SampleBaseUrl,
        string DocumentationBaseUrl,
        Dictionary<string, string> Files);
    private sealed record ComponentReference(string Name, string FullName, string Category, string Sample, string[] GenericParameters, string? Summary, ParameterReference[] Parameters);
    private sealed record ParameterReference(string Name, string Type, bool Cascading, bool RenderFragment, bool Bindable, string? Summary, bool Settable);
    private sealed record TypeReference(string Name, string FullName, string Kind, string[] GenericParameters, string[] Values, string? Summary, MemberReference[] Members);
    private sealed record MemberReference(string Name, string Kind, string Type, string? Summary);

    private sealed class FixtureComponent<T> : ComponentBase
    {
        [Parameter] public T? Value { get; set; }
        [Parameter] public EventCallback<T?> ValueChanged { get; set; }
        [Parameter] public RenderFragment? ChildContent { get; set; }
        [CascadingParameter] public string? Theme { get; set; }
    }

    private interface FixtureService
    {
        void Reset();
    }

    private enum FixtureKind
    {
        Standard,
        Accent
    }
}
