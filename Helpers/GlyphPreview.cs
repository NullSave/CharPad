using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace NullSave.CharPad
{

    /// <summary>The large view of the selected character; the glyph scales with the pane.</summary>
    internal sealed class GlyphPreview : FrameworkElement
    {

        #region Fields

        private GlyphTypeface? m_glyphs;
        private int m_code = -1;

        #endregion

        #region Public Methods

        /// <summary>Shows <paramref name="code"/> drawn from <paramref name="glyphs"/>; pass null or -1 to clear the preview.</summary>
        public void Show(GlyphTypeface? glyphs, int code)
        {
            m_glyphs = glyphs;
            m_code = code;
            InvalidateVisual();
        }

        #endregion

        #region Protected Methods

        /// <inheritdoc/>
        protected override void OnRender(DrawingContext dc)
        {
            var bounds = new Rect(RenderSize);
            dc.DrawRectangle(SystemColors.WindowBrush, null, bounds);
            if (m_glyphs == null || m_code < 0 || bounds.Width < 16 || bounds.Height < 16)
            {
                return;
            }

            float pixelsPerDip = (float)VisualTreeHelper.GetDpi(this).PixelsPerDip;
            double side = Math.Min(bounds.Width, bounds.Height);
            double emSize = side * 0.6, pad = side * 0.08;
            var layout = GlyphPainter.Measure(m_glyphs, m_code, emSize, bounds.Width - 2 * pad, bounds.Height - 2 * pad, pixelsPerDip);

            if (layout.Ink.IsEmpty)
            {
                var text = new FormattedText("(blank glyph)", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                    new Typeface(CharGrid.DEFAULT_FONT_NAME), 16, SystemColors.GrayTextBrush, pixelsPerDip);

                dc.DrawText(text, new Point((bounds.Width - text.Width) / 2, (bounds.Height - text.Height) / 2));
                return;
            }

            GlyphPainter.Draw(dc, m_glyphs, layout, emSize, bounds, SystemColors.WindowTextBrush, pixelsPerDip, pad);
        }

        #endregion

    }
}
