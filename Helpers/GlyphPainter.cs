using System.Windows;
using System.Windows.Media;

namespace NullSave.CharPad
{

    /// <summary>
    /// Draws single glyphs straight from a font's glyph table, so what you see is the selected font's own glyph and never a
    /// fallback font's substitute.
    /// </summary>
    internal static class GlyphPainter
    {

        #region Public Methods

        /// <summary>
        /// Looks up the glyph for <paramref name="code"/> and measures its ink at <paramref name="emSize"/>, shrinking it if
        /// needed to fit <paramref name="fitWidth"/> × <paramref name="fitHeight"/>. The ink box is empty for blank glyphs.
        /// </summary>
        public static GlyphLayout Measure(GlyphTypeface glyphs, int code, double emSize, double fitWidth, double fitHeight, float pixelsPerDip)
        {
            if (emSize <= 0 || !glyphs.CharacterToGlyphMap.TryGetValue(code, out ushort index))
            {
                return new GlyphLayout(0, 1, Rect.Empty);
            }

            Rect ink = Run(glyphs, index, emSize, default, pixelsPerDip).ComputeInkBoundingBox();
            if (ink.IsEmpty || (ink.Width <= 0 && ink.Height <= 0))
            {
                return new GlyphLayout(index, 1, Rect.Empty);
            }

            double scale = Math.Min(1, Math.Min(fitWidth / Math.Max(ink.Width, 1e-3), fitHeight / Math.Max(ink.Height, 1e-3)));
            if (scale < 1)
            {
                ink = new Rect(ink.X * scale, ink.Y * scale, ink.Width * scale, ink.Height * scale);
            }

            return new GlyphLayout(index, scale, ink);
        }

        /// <summary>
        /// Centers the glyph horizontally in <paramref name="cell"/>. Vertically it sits on the font's own baseline (so
        /// punctuation keeps its natural height) unless that would push the ink outside the padded cell.
        /// </summary>
        public static void Draw(DrawingContext dc, GlyphTypeface glyphs, GlyphLayout layout, double emSize, Rect cell, Brush brush, float pixelsPerDip, double pad)
        {
            Rect ink = layout.Ink;
            if (ink.IsEmpty)
            {
                return;
            }

            double size = emSize * layout.Scale;
            double x = cell.X + cell.Width / 2 - (ink.X + ink.Width / 2);
            double y;

            if (layout.Scale < 1)
            {
                y = cell.Y + cell.Height / 2 - (ink.Y + ink.Height / 2);
            }
            else
            {
                y = cell.Y + (cell.Height - glyphs.Height * size) / 2 + glyphs.Baseline * size;
                double top = y + ink.Top;
                if (top < cell.Top + pad)
                {
                    y += cell.Top + pad - top;
                }

                double bottom = y + ink.Bottom;
                if (bottom > cell.Bottom - pad)
                {
                    y -= bottom - (cell.Bottom - pad);
                }
            }

            dc.DrawGlyphRun(brush, Run(glyphs, layout.Index, size, new Point(x, y), pixelsPerDip));
        }

        #endregion

        #region Private Methods
        private static GlyphRun Run(GlyphTypeface glyphs, ushort index, double emSize, Point origin, float pixelsPerDip)
        {
            return new(glyphs, 0, false, emSize, pixelsPerDip, new[] { index }, origin, new[] { glyphs.AdvanceWidths[index] * emSize }, null, null, null, null, null, null);
        }

        #endregion

    }
}
