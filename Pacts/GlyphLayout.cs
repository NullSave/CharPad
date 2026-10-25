using System.Windows;

namespace NullSave.CharPad
{

    /// <summary>A glyph's index plus its ink box (relative to the baseline origin) after shrinking it to fit, if it had to be.</summary>
    internal readonly record struct GlyphLayout(ushort Index, double Scale, Rect Ink);

}
