using Microsoft.AspNetCore.Razor.Language;

namespace FluentKit.Sample.Shared.Services;

/// <summary>
/// A single in-memory ".razor" source file fed to the Razor engine. There is no real
/// filesystem in Blazor WASM, so this stands in for what would normally be a file on disk.
/// </summary>
internal sealed class InMemoryRazorProjectItem : RazorProjectItem
{
    private readonly string _content;

    public InMemoryRazorProjectItem(string filePath, string fileKind, string content)
    {
        FilePath = filePath;
        FileKind = fileKind;
        _content = content;
    }

    public override string BasePath => "/";
    public override string FilePath { get; }
    public override string PhysicalPath => FilePath;
    public override bool Exists => true;
    public override string FileKind { get; }

    public override Stream Read()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(_content);
        return new MemoryStream(bytes, writable: false);
    }
}

/// <summary>Minimal <see cref="RazorProjectFileSystem"/> that serves exactly one in-memory item.</summary>
internal sealed class InMemoryRazorProjectFileSystem(InMemoryRazorProjectItem item) : RazorProjectFileSystem
{
    public override IEnumerable<RazorProjectItem> EnumerateItems(string basePath) => [item];

    public override RazorProjectItem GetItem(string path) => item;

    public override RazorProjectItem GetItem(string path, string? fileKind) => item;
}
