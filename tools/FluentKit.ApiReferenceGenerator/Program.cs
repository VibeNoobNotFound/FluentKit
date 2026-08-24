using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using Microsoft.AspNetCore.Components;

return await ApiReferenceGenerator.RunAsync(args);

internal static class ApiReferenceGenerator
{
    // Keep generated JSON stable so it can be compared in CI and copied byte-for-byte into
    // the versioned API skill bundle.
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

        if (options.ContainsKey("verify"))
        {
            // Verification never writes tracked files. It is used by PR/release CI to catch
            // developers who changed the assembly without regenerating the checked-in output.
            var ok = CompareFile(jsonPath, json) & CompareFile(markdownPath, markdown);
            return ok ? 0 : 1;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(jsonPath))!);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(markdownPath))!);
        await File.WriteAllTextAsync(jsonPath, json, Encoding.UTF8);
        await File.WriteAllTextAsync(markdownPath, markdown, Encoding.UTF8);
        return 0;
    }

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

    private static void PrintUsage() => Console.WriteLine("Usage: --assembly PATH --manifest PATH --json PATH --markdown PATH [--xml PATH] [--verify] | --self-test");

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
