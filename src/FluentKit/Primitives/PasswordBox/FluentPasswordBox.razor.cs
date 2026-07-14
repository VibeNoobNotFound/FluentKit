using Microsoft.AspNetCore.Components;

namespace FluentKit.Primitives;

/// <summary>
/// Mirrors WinUI's PasswordBox, specifically its reveal-button variant: a right-aligned eye icon
/// button that temporarily shows the plaintext value while held/toggled, rather than a bare
/// <c>&lt;input type="password"&gt;</c> with no way to check what you typed. Composes
/// <see cref="FluentTextBox"/> + <see cref="FluentTextBoxButton"/> the same way NumberBox composes
/// TextBox + TextBoxButton — swapping the underlying input's Type between "password" and "text"
/// rather than reimplementing input chrome from scratch.
/// </summary>
public partial class FluentPasswordBox : ComponentBase
{
    [Parameter]
    public string? Value { get; set; }

    [Parameter]
    public EventCallback<string?> ValueChanged { get; set; }

    [Parameter]
    public string? Header { get; set; }

    [Parameter]
    public string? Placeholder { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private bool _revealed;

    private Task OnValueChanged(string? value)
    {
        Value = value;
        return ValueChanged.InvokeAsync(value);
    }

    private void ToggleRevealAsync() => _revealed = !_revealed;
}
