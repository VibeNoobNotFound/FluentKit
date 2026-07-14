namespace FluentKit.Composite;

/// <summary>
/// Controls how NumberBox's spin buttons are presented — see FluentNumberBox.razor.cs remarks.
/// </summary>
public enum NumberBoxSpinButtonMode
{
    /// <summary>Two small buttons side by side, permanently visible inside the TextBox's Buttons slot.</summary>
    Compact,

    /// <summary>
    /// No buttons inside the box at rest. On focus, a larger stacked up/down control pops out from
    /// the box's edge (bigger touch targets, doesn't crowd the input at rest).
    /// </summary>
    Expanded
}
