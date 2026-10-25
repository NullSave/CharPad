using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace NullSave.CharPad
{

    /// <summary>
    /// The character table. Cells are sized from <see cref="GlyphSize"/> and the number of columns follows the control's
    /// width, so the grid reflows as the window is resized. Only visible rows are drawn, which keeps fonts with tens of
    /// thousands of glyphs responsive.
    /// </summary>
    internal sealed class CharGrid : FrameworkElement
    {

        #region Constants

        public const string DEFAULT_FONT_NAME = "Arial";
        private const double PAD = 4;

        #endregion

        #region Events

        /// <summary>Raised when the selected character changes, including when <see cref="SetChars"/> replaces the list.</summary>
        public event EventHandler? SelectionChanged;

        /// <summary>Raised when the selected character is double-clicked or Enter/Space is pressed on it.</summary>
        public event EventHandler? CharActivated;

        /// <summary>Raised on Ctrl+mouse wheel; the argument is +1 to enlarge the glyphs or -1 to shrink them.</summary>
        public event EventHandler<int>? ZoomRequested;

        #endregion

        #region Fields

        private readonly ScrollBar m_bar = new() { Orientation = Orientation.Vertical, SmallChange = 1 };
        private readonly ToolTip m_tip = new() { FontSize = 16 };
        private readonly Dictionary<int, GlyphLayout> m_layouts = new();
        private readonly Brush m_hoverBrush;
        private GlyphTypeface? m_glyphs;
        private double m_glyphSize = 32;
        private int[] m_chars = [];
        private int m_selected = -1;
        private int m_hover = -1;
        private int m_firstRow;
        private int m_columns = 1;
        private int m_visibleRows = 1;
        private double m_cellWidth;
        private double m_cellHeight;
        private bool m_scrollToSelection;

        #endregion

        #region Properties

        /// <summary>The font whose glyphs are drawn; null shows a "can't be displayed" message instead.</summary>
        public GlyphTypeface? Glyphs
        {
            get => m_glyphs;
            set
            {
                m_glyphs = value;
                m_layouts.Clear();
                InvalidateVisual();
            }
        }

        /// <summary>Glyph em size in device-independent pixels.</summary>
        public double GlyphSize
        {
            get => m_glyphSize;
            set
            {
                m_glyphSize = value;
                m_layouts.Clear();
                m_scrollToSelection = true;
                InvalidateArrange();
                InvalidateVisual();
            }
        }

        /// <summary>Number of characters currently displayed.</summary>
        public int Count => m_chars.Length;

        /// <summary>The selected code point, or -1 when nothing is selected.</summary>
        public int SelectedCode => m_selected >= 0 ? m_chars[m_selected] : -1;

        /// <inheritdoc/>
        protected override int VisualChildrenCount => 1;

        #endregion

        #region Constructors

        /// <summary>Creates an empty grid with its scroll bar and per-cell tooltip set up.</summary>
        public CharGrid()
        {
            Focusable = true;
            FocusVisualStyle = null;
            ClipToBounds = true;

            var highlight = SystemColors.HighlightColor;
            m_hoverBrush = new SolidColorBrush(Color.FromArgb(0x38, highlight.R, highlight.G, highlight.B));
            m_hoverBrush.Freeze();

            AddVisualChild(m_bar);
            m_bar.Scroll += (_, _) => SetFirstRow((int)Math.Round(m_bar.Value));

            m_tip.Placement = PlacementMode.Relative;
            m_tip.PlacementTarget = this;
            ToolTip = m_tip;
            ToolTipService.SetInitialShowDelay(this, 500);
            ToolTipOpening += (_, e) => { if (m_hover < 0) e.Handled = true; };
        }

        #endregion

        #region Public Methods

        /// <summary>Replaces the displayed characters (sorted ascending), keeping the current selection when it is still present.</summary>
        public void SetChars(int[] value)
        {
            int keep = SelectedCode;
            m_chars = value;
            m_selected = m_chars.Length == 0 ? -1 : Math.Max(0, Array.BinarySearch(m_chars, keep));
            m_hover = -1;
            m_firstRow = 0;
            m_scrollToSelection = true;
            InvalidateArrange();
            InvalidateVisual();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Selects and scrolls to <paramref name="code"/>; returns false if it isn't among the displayed characters.</summary>
        public bool SelectCode(int code)
        {
            int index = Array.BinarySearch(m_chars, code);
            if (index < 0)
            {
                return false;
            }

            Select(index);

            return true;
        }

        #endregion

        #region Protected Methods

        /// <inheritdoc/>
        protected override Visual GetVisualChild(int index)
        {
            return m_bar;
        }

        /// <inheritdoc/>
        protected override Size MeasureOverride(Size availableSize)
        {
            m_bar.Measure(availableSize);
            return new Size(0, 0);
        }

        /// <inheritdoc/>
        protected override Size ArrangeOverride(Size finalSize)
        {
            double barWidth = SystemParameters.VerticalScrollBarWidth;
            double gridWidth = Math.Max(0, finalSize.Width - barWidth);
            int topIndex = m_firstRow * m_columns;

            m_cellHeight = Math.Ceiling(m_glyphSize * 1.5 + 2 * PAD);
            m_columns = Math.Max(1, (int)(gridWidth / m_cellHeight));
            m_cellWidth = gridWidth / m_columns;
            m_visibleRows = Math.Max(1, (int)(finalSize.Height / m_cellHeight));

            int rows = (m_chars.Length + m_columns - 1) / m_columns;
            int maxFirstRow = Math.Max(0, rows - m_visibleRows);

            m_bar.Maximum = maxFirstRow;
            m_bar.ViewportSize = m_visibleRows;
            m_bar.LargeChange = m_visibleRows;
            m_bar.IsEnabled = maxFirstRow > 0;

            m_firstRow = Math.Clamp(topIndex / m_columns, 0, maxFirstRow);
            m_bar.Value = m_firstRow;
            if (m_scrollToSelection)
            {
                m_scrollToSelection = false;
                EnsureVisible(m_selected);
            }

            m_bar.Arrange(new Rect(gridWidth, 0, barWidth, finalSize.Height));
            return finalSize;
        }

        /// <inheritdoc/>
        protected override void OnRender(DrawingContext dc)
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            float pixelsPerDip = (float)dpi.PixelsPerDip;
            double gridWidth = m_columns * m_cellWidth, height = RenderSize.Height;
            dc.DrawRectangle(SystemColors.WindowBrush, null, new Rect(0, 0, gridWidth, height));

            if (m_glyphs == null || m_chars.Length == 0)
            {
                DrawMessage(dc, m_glyphs == null ? "This font can't be displayed." : "No characters to show.", gridWidth, height, pixelsPerDip);
                return;
            }

            int rowsShown = (int)Math.Ceiling(height / m_cellHeight);
            int rowsWithChars = Math.Min(rowsShown, (m_chars.Length - m_firstRow * m_columns + m_columns - 1) / m_columns);
            for (int r = 0; r < rowsWithChars; r++)
            {
                for (int c = 0; c < m_columns; c++)
                {
                    int i = (m_firstRow + r) * m_columns + c;
                    if (i >= m_chars.Length)
                    {
                        break;
                    }

                    var cell = new Rect(c * m_cellWidth, r * m_cellHeight, m_cellWidth, m_cellHeight);
                    Brush foreground = SystemColors.WindowTextBrush;
                    if (i == m_selected)
                    {
                        dc.DrawRectangle(SystemColors.HighlightBrush, null, cell);
                        foreground = SystemColors.HighlightTextBrush;
                    }
                    else if (i == m_hover)
                    {
                        dc.DrawRectangle(m_hoverBrush, null, cell);
                    }

                    GlyphPainter.Draw(dc, m_glyphs, Layout(m_chars[i], pixelsPerDip), m_glyphSize, cell, foreground, pixelsPerDip, PAD);
                }
            }

            // Grid lines as device-pixel-aligned 1px rectangles so they stay crisp at any display scaling.
            var lineBrush = SystemColors.ControlDarkBrush;
            double px = 1 / dpi.DpiScaleX, py = 1 / dpi.DpiScaleY;
            double bottom = Math.Min(height, rowsWithChars * m_cellHeight);
            for (int c = 0; c <= m_columns; c++)
            {
                double x = Math.Min(Math.Round(c * m_cellWidth * dpi.DpiScaleX) / dpi.DpiScaleX, gridWidth - px);
                dc.DrawRectangle(lineBrush, null, new Rect(x, 0, px, bottom));
            }
            for (int r = 0; r <= rowsWithChars; r++)
            {
                double y = Math.Min(Math.Round(r * m_cellHeight * dpi.DpiScaleY) / dpi.DpiScaleY, bottom - py);
                dc.DrawRectangle(lineBrush, null, new Rect(0, y, gridWidth, py));
            }
        }

        /// <inheritdoc/>
        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            m_layouts.Clear();
            InvalidateVisual();
            base.OnDpiChanged(oldDpi, newDpi);
        }

        /// <inheritdoc/>
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            Focus();
            int i = IndexAt(e.GetPosition(this));
            if (i >= 0)
            {
                Select(i);
                if (e.ClickCount == 2)
                {
                    CharActivated?.Invoke(this, EventArgs.Empty);
                }
                e.Handled = true;
            }
            base.OnMouseLeftButtonDown(e);
        }

        /// <inheritdoc/>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            var p = e.GetPosition(this);

            UpdateHover(p);

            if (e.LeftButton == MouseButtonState.Pressed && IsKeyboardFocused && IndexAt(p) is int i and >= 0)
            {
                Select(i);
            }

            base.OnMouseMove(e);
        }

        /// <inheritdoc/>
        protected override void OnMouseLeave(MouseEventArgs e)
        {
            UpdateHover(new Point(-1, -1));
            base.OnMouseLeave(e);
        }

        /// <inheritdoc/>
        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            int direction = Math.Sign(e.Delta);
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                ZoomRequested?.Invoke(this, direction);
            }
            else
            {
                SetFirstRow(m_firstRow - direction * (m_visibleRows >= 6 ? 3 : 1));
            }
            e.Handled = true;
        }

        /// <inheritdoc/>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (m_chars.Length == 0)
            {
                return;
            }

            int i = Math.Max(m_selected, 0), last = m_chars.Length - 1;
            bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
            int? target = e.Key switch
            {
                Key.Left => i - 1,
                Key.Right => i + 1,
                Key.Up => i >= m_columns ? i - m_columns : i,
                Key.Down => i + m_columns <= last ? i + m_columns : i,
                Key.PageUp => i - m_columns * m_visibleRows,
                Key.PageDown => i + m_columns * m_visibleRows,
                Key.Home => ctrl ? 0 : i - i % m_columns,
                Key.End => ctrl ? last : i - i % m_columns + m_columns - 1,
                _ => null,
            };

            if (target is int t)
            {
                Select(Math.Clamp(t, 0, last));
                e.Handled = true;
            }
            else if (e.Key is Key.Enter or Key.Space)
            {
                CharActivated?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
            }

            base.OnKeyDown(e);
        }

        #endregion

        #region Private Methods

        private void Select(int index)
        {
            if (index != m_selected)
            {
                m_selected = index;
                InvalidateVisual();
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }

            EnsureVisible(index);
        }

        private void EnsureVisible(int index)
        {
            if (index < 0)
            {
                return;
            }

            int row = index / m_columns;
            if (row < m_firstRow)
            {
                SetFirstRow(row);
            }
            else if (row >= m_firstRow + m_visibleRows)
            {
                SetFirstRow(row - m_visibleRows + 1);
            }
        }

        private void SetFirstRow(int row)
        {
            row = Math.Clamp(row, 0, (int)m_bar.Maximum);
            m_bar.Value = row;
            if (row != m_firstRow)
            {
                m_firstRow = row;
                UpdateHover(Mouse.GetPosition(this));
                InvalidateVisual();
            }
        }

        private int IndexAt(Point p)
        {
            if (p.X < 0 || p.Y < 0 || p.X >= m_columns * m_cellWidth || m_cellWidth <= 0)
            {
                return -1;
            }

            int c = (int)(p.X / m_cellWidth), r = (int)(p.Y / m_cellHeight);
            int i = (m_firstRow + r) * m_columns + c;

            return c < m_columns && i < m_chars.Length ? i : -1;
        }

        private void UpdateHover(Point p)
        {
            int i = IsMouseOver ? IndexAt(p) : -1;
            if (i == m_hover)
            {
                return;
            }

            m_hover = i;
            if (i >= 0)
            {
                m_tip.Content = $"U+{m_chars[i]:X4}   {UnicodeInfo.Name(m_chars[i])}";
                m_tip.HorizontalOffset = i % m_columns * m_cellWidth;
                m_tip.VerticalOffset = (i / m_columns - m_firstRow + 1) * m_cellHeight + 2;
            }
            else
            {
                m_tip.IsOpen = false;
            }

            InvalidateVisual();
        }

        private GlyphLayout Layout(int code, float pixelsPerDip)
        {
            if (!m_layouts.TryGetValue(code, out var layout))
            {
                double fit = m_cellHeight - 2 * PAD;
                layout = GlyphPainter.Measure(m_glyphs!, code, m_glyphSize, fit, fit, pixelsPerDip);
                m_layouts[code] = layout;
            }

            return layout;
        }

        private static void DrawMessage(DrawingContext dc, string message, double width, double height, float pixelsPerDip)
        {
            var text = new FormattedText(message, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface(DEFAULT_FONT_NAME), 18, SystemColors.GrayTextBrush, pixelsPerDip);
            dc.DrawText(text, new Point(Math.Max(0, (width - text.Width) / 2), Math.Max(0, (height - text.Height) / 2)));
        }

        #endregion

    }
}