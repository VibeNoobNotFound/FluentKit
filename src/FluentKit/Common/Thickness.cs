namespace FluentKit.Common;

/// <summary>
/// Mirrors XAML's <c>Thickness</c> (used for <c>Padding</c>/<c>Margin</c> in WPF/WinUI) — same
/// three constructor shapes (uniform, horizontal/vertical, four-value), same Left/Top/Right/Bottom
/// field names. Values are CSS pixels. Renders to a CSS <c>padding</c> shorthand via
/// <see cref="ToCss"/> — note CSS shorthand order is top/right/bottom/left, not left/top/right/bottom,
/// so the constructor argument order and the emitted order intentionally differ; that's a CSS
/// convention mismatch inherited from the platform, not a bug here.
/// </summary>
public readonly struct Thickness : IEquatable<Thickness>
{
    public double Left { get; }
    public double Top { get; }
    public double Right { get; }
    public double Bottom { get; }

    /// <summary>Same value on all four sides — e.g. <c>new Thickness(8)</c>.</summary>
    public Thickness(double uniform) : this(uniform, uniform, uniform, uniform) { }

    /// <summary>Horizontal (left+right) and vertical (top+bottom) — e.g. <c>new Thickness(16, 8)</c>.</summary>
    public Thickness(double horizontal, double vertical) : this(horizontal, vertical, horizontal, vertical) { }

    /// <summary>All four sides independently, XAML order: left, top, right, bottom.</summary>
    public Thickness(double left, double top, double right, double bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    public static Thickness Zero { get; } = new(0);

    /// <summary>Lets a plain number be used where a Thickness is expected — <c>Padding="12"</c> means uniform 12px.</summary>
    public static implicit operator Thickness(double uniform) => new(uniform);

    /// <summary>CSS <c>padding</c> shorthand value, e.g. <c>"4px 8px 4px 8px"</c> (top right bottom left).</summary>
    public string ToCss() =>
        $"{Top.ToString(System.Globalization.CultureInfo.InvariantCulture)}px " +
        $"{Right.ToString(System.Globalization.CultureInfo.InvariantCulture)}px " +
        $"{Bottom.ToString(System.Globalization.CultureInfo.InvariantCulture)}px " +
        $"{Left.ToString(System.Globalization.CultureInfo.InvariantCulture)}px";

    public bool Equals(Thickness other) =>
        Left.Equals(other.Left) && Top.Equals(other.Top) && Right.Equals(other.Right) && Bottom.Equals(other.Bottom);

    public override bool Equals(object? obj) => obj is Thickness other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Left, Top, Right, Bottom);
    public static bool operator ==(Thickness left, Thickness right) => left.Equals(right);
    public static bool operator !=(Thickness left, Thickness right) => !left.Equals(right);
}
