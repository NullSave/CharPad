using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace NullSave.CharPad
{

    public partial class MainWindow : Window
    {

        #region Constants

        private const double MIN_SIZE = 6;
        private const double MAX_SIZE = 300;
        private const double DEFAULT_SIZE = 24;

        #endregion

        #region Fields

        private static readonly double[] m_sizes = [8, 9, 10, 11, 12, 14, 16, 18, 20, 22, 24, 28, 32, 36, 40, 48, 56, 64, 72, 96, 120, 144];
        private static readonly string[] m_groupModes = ["All", "Unicode block", "Unicode category"];
        private static readonly char[] m_nameSeparators = [' ', '-'];

        private readonly Settings m_settings = Settings.Load();
        private readonly List<FontEntry> m_fonts = FontCatalog.Load();
        private readonly DispatcherTimer m_searchTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };
        private GlyphTypeface? m_glyphs;
        private int[] m_fontChars = [];
        private double m_sizePt;
        private bool m_ready;
        private bool m_filtering;

        #endregion

        #region Constructors

        /// <summary>Builds the window and restores the last session's placement, font, size and view options.</summary>
        public MainWindow()
        {
            InitializeComponent();
            RestorePlacement();

            FontCombo.ItemsSource = m_fonts;
            FontCombo.SelectedItem = m_fonts.FirstOrDefault(f => f.Name == m_settings.Font) ?? m_fonts.FirstOrDefault(f => f.Name == "Segoe UI") ?? m_fonts.FirstOrDefault();
            SizeCombo.ItemsSource = m_sizes;
            SetSize(m_settings.Size);
            CharsetCombo.ItemsSource = CodePages.All;
            CharsetCombo.SelectedItem = CodePages.All.FirstOrDefault(c => c.Id == m_settings.Charset) ?? CodePages.All[0];
            GroupByCombo.ItemsSource = m_groupModes;
            GroupByCombo.SelectedIndex = 0;
            AdvancedCheck.IsChecked = m_settings.Advanced;
            AdvancedPanel.Visibility = m_settings.Advanced ? Visibility.Visible : Visibility.Collapsed;
            PreviewColumn.Width = new GridLength(Math.Clamp(m_settings.PreviewWidth, 160, 900));

            FontCombo.SelectionChanged += (_, _) => LoadFont();
            SizeCombo.SelectionChanged += (_, _) => { if (SizeCombo.SelectedItem is double size) SetSize(size); };
            SizeCombo.LostKeyboardFocus += (_, _) => CommitSizeText();
            SizeCombo.PreviewKeyDown += (_, e) =>
            {
                if (e.Key != Key.Enter) return;
                CommitSizeText();
                Chars.Focus();
                e.Handled = true;
            };
            SmallerButton.Click += (_, _) => Zoom(-1);
            LargerButton.Click += (_, _) => Zoom(+1);

            Chars.SelectionChanged += (_, _) => ShowSelection();
            Chars.CharActivated += (_, _) => AddSelectedToCopyText();
            Chars.ZoomRequested += (_, direction) => Zoom(direction);
            SelectButton.Click += (_, _) => AddSelectedToCopyText();
            CopyButton.Click += (_, _) => CopyClicked();

            AdvancedCheck.Click += (_, _) =>
            {
                AdvancedPanel.Visibility = AdvancedCheck.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                ApplyFilters(rebuildGroups: true);
            };
            CharsetCombo.SelectionChanged += (_, _) => ApplyFilters(rebuildGroups: true);
            GroupByCombo.SelectionChanged += (_, _) => ApplyFilters(rebuildGroups: true);
            GroupCombo.SelectionChanged += (_, _) => ApplyFilters(rebuildGroups: false);
            SearchText.TextChanged += (_, _) => { m_searchTimer.Stop(); m_searchTimer.Start(); };
            SearchText.PreviewKeyDown += (_, e) => { if (e.Key == Key.Enter) { m_searchTimer.Stop(); ApplyFilters(rebuildGroups: false); e.Handled = true; } };
            m_searchTimer.Tick += (_, _) => { m_searchTimer.Stop(); ApplyFilters(rebuildGroups: false); };
            SearchButton.Click += (_, _) => { m_searchTimer.Stop(); ApplyFilters(rebuildGroups: false); };
            ClearButton.Click += (_, _) => SearchText.Clear();
            GotoText.PreviewKeyDown += (_, e) => { if (e.Key == Key.Enter) { GoTo(); e.Handled = true; } };
            GotoButton.Click += (_, _) => GoTo();

            PreviewKeyDown += WindowPreviewKeyDown;
            Closing += (_, _) => SaveSettings();
            Loaded += (_, _) => Chars.Focus();

            m_ready = true;
            LoadFont();
        }

        #endregion

        #region Private Methods

        private void LoadFont()
        {
            if (!m_ready)
            {
                return;
            }

            var font = FontCombo.SelectedItem as FontEntry;
            m_glyphs = font == null ? null : FontCatalog.GetGlyphs(font.Family);
            m_fontChars = m_glyphs == null ? [] : DisplayableChars(m_glyphs);
            Chars.Glyphs = m_glyphs;
            CopyText.FontFamily = font?.Family ?? FontFamily;
            ApplyFilters(rebuildGroups: true);
        }

        private static int[] DisplayableChars(GlyphTypeface glyphs)
        {
            var map = glyphs.CharacterToGlyphMap;
            var codes = map.Keys.Where(UnicodeInfo.IsDisplayable);

            // Symbol fonts (Wingdings, Webdings...) are mapped at U+F020-F0FF and WPF also aliases them to 0x20-0xFF.
            // Show each glyph once, at its private-use code, as charmap does.
            if (glyphs.Symbol)
            {
                codes = codes.Where(c => c > 0xFF || !map.ContainsKey(0xF000 + c));
            }

            return codes.Order().ToArray();
        }

        private bool IsSymbolCode(int code)
        {
            return m_glyphs?.Symbol == true && code is >= 0xF000 and <= 0xF0FF;
        }

        private void ApplyFilters(bool rebuildGroups)
        {
            if (!m_ready || m_filtering)
            {
                return;
            }

            m_filtering = true;

            try
            {
                bool advanced = AdvancedCheck.IsChecked == true;
                int groupMode = advanced ? GroupByCombo.SelectedIndex : 0;

                int[] available = m_fontChars;
                if (advanced && CharsetCombo.SelectedItem is CodePage { Id: > 0 } codePage)
                {
                    var encodable = CodePages.Chars(codePage.Id);
                    available = available.Where(encodable.Contains).ToArray();
                }

                if (rebuildGroups)
                {
                    RebuildGroups(available, groupMode);
                }

                IEnumerable<int> shown = available;

                if (groupMode > 0 && GroupCombo.SelectedItem is CharGroup group)
                {
                    shown = shown.Where(c => GroupKey(c, groupMode) == group.Key);
                }

                if (advanced)
                {
                    shown = Search(shown, SearchText.Text);
                }

                Chars.SetChars(shown.ToArray());
                CountText.Text = Chars.Count == m_fontChars.Length ? $"{Chars.Count:N0} characters" : $"{Chars.Count:N0} of {m_fontChars.Length:N0} characters";
            }
            finally
            {
                m_filtering = false;
            }
        }

        private void RebuildGroups(int[] available, int groupMode)
        {
            string? previous = (GroupCombo.SelectedItem as CharGroup)?.Key;

            if (groupMode == 0)
            {
                GroupCombo.ItemsSource = null;
                GroupCombo.IsEnabled = false;
                return;
            }

            var groups = available.GroupBy(c => GroupKey(c, groupMode)).Select(g => new CharGroup(g.Key, g.Count()));

            if (groupMode == 2)
            {
                groups = groups.OrderBy(g => g.Key);
            }

            var list = groups.ToList();
            GroupCombo.ItemsSource = list;
            GroupCombo.IsEnabled = list.Count > 0;
            GroupCombo.SelectedItem = list.FirstOrDefault(g => g.Key == previous) ?? list.FirstOrDefault();
        }

        private static string GroupKey(int code, int groupMode)
        {
            return groupMode == 1 ? UnicodeInfo.Block(code) : UnicodeInfo.Category(code);
        }

        private static IEnumerable<int> Search(IEnumerable<int> chars, string query)
        {
            query = query.Trim();
            if (query.Length == 0)
            {
                return chars;
            }

            if (query.StartsWith("U+", StringComparison.OrdinalIgnoreCase) && TryParseCode(query, out int code))
            {
                return chars.Where(c => c == code);
            }

            // A single pasted symbol finds itself; plain ASCII letters and digits are treated as name text.
            if (query.EnumerateRunes().Count() == 1 && !(char.IsAsciiLetterOrDigit(query[0]) && query.Length == 1))
            {
                int rune = char.ConvertToUtf32(query, 0);
                return chars.Where(c => c == rune);
            }

            // Each query word must start a word of the name, so "arrow" finds RIGHTWARDS ARROW but not NARROW NO-BREAK SPACE.
            string[] words = query.Split(m_nameSeparators, StringSplitOptions.RemoveEmptyEntries);
            return chars.Where(c =>
            {
                string[] nameWords = UnicodeInfo.Name(c).Split(m_nameSeparators, StringSplitOptions.RemoveEmptyEntries);
                return words.All(w => nameWords.Any(n => n.StartsWith(w, StringComparison.OrdinalIgnoreCase)));
            });
        }

        private static bool TryParseCode(string text, out int code)
        {
            text = text.Trim();
            if (text.StartsWith("U+", StringComparison.OrdinalIgnoreCase) || text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                text = text[2..];
            }
            return int.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out code) && code is >= 0 and <= 0x10FFFF;
        }

        private void GoTo()
        {
            if (!TryParseCode(GotoText.Text, out int code))
            {
                StatusText.Text = "Enter a hexadecimal code such as 00A9 or U+1F600.";
                return;
            }

            if (Chars.SelectCode(code))
            {
                Chars.Focus();
            }
            else if (Array.BinarySearch(m_fontChars, code) >= 0)
            {
                StatusText.Text = $"U+{code:X4} is in this font but hidden by the current character set, group or search.";
            }
            else
            {
                StatusText.Text = $"This font has no character U+{code:X4}.";
            }
        }

        private void ShowSelection()
        {
            int code = Chars.SelectedCode;
            Preview.Show(m_glyphs, code);
            if (code < 0)
            {
                NameText.Text = DetailsText.Text = StatusText.Text = KeystrokeText.Text = "";
                return;
            }

            string name = UnicodeInfo.Name(code);
            string text = char.ConvertFromUtf32(code);
            string keystroke = CodePages.Keystroke(IsSymbolCode(code) ? code & 0xFF : code);
            string codeLabel = IsSymbolCode(code) ? $"U+{code:X4} (0x{code & 0xFF:X2})" : $"U+{code:X4}";

            NameText.Text = name.Length > 0 ? name : "(unnamed)";
            var details = new StringBuilder()
                .AppendLine($"{codeLabel}   ·   Decimal {code}")
                .AppendLine($"Block: {UnicodeInfo.Block(code)}")
                .AppendLine($"Category: {UnicodeInfo.Category(code)}")
                .AppendLine($"UTF-8: {string.Join(" ", Encoding.UTF8.GetBytes(text).Select(b => b.ToString("X2")))}")
                .AppendLine($"UTF-16: {string.Join(" ", text.Select(ch => ((int)ch).ToString("X4")))}")
                .Append($"HTML: &#{code};   &#x{code:X};");
            if (keystroke.Length > 0)
            {
                details.AppendLine().Append($"Keystroke: {keystroke}");
            }
            DetailsText.Text = details.ToString();

            StatusText.Text = $"{codeLabel}: {name}";
            KeystrokeText.Text = keystroke.Length > 0 ? $"Keystroke: {keystroke}" : "";
        }

        private void AddSelectedToCopyText()
        {
            int code = Chars.SelectedCode;
            if (code < 0)
            {
                return;
            }

            CopyText.SelectedText = char.ConvertFromUtf32(code);
            CopyText.Select(CopyText.SelectionStart + CopyText.SelectionLength, 0);
        }

        private void CopyClicked()
        {
            string text = CopyText.SelectionLength > 0 ? CopyText.SelectedText : CopyText.Text;
            if (text.Length == 0 && Chars.SelectedCode >= 0)
            {
                text = char.ConvertFromUtf32(Chars.SelectedCode);
            }

            CopyToClipboard(text);
        }

        private void CopyToClipboard(string text)
        {
            if (text.Length == 0)
            {
                return;
            }

            // Like charmap, include RTF naming the font so pasting into Word or WordPad keeps it.
            var font = FontCombo.SelectedItem as FontEntry;
            var data = new DataObject();
            data.SetData(DataFormats.UnicodeText, text);
            if (font != null)
            {
                data.SetData(DataFormats.Rtf, ToRtf(text, font.Name, m_glyphs?.Symbol == true));
            }

            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    Clipboard.SetDataObject(data, copy: true);
                    StatusText.Text = text.EnumerateRunes().Count() == 1 ? $"Copied {text} to the clipboard." : $"Copied {text.EnumerateRunes().Count()} characters to the clipboard.";
                    return;
                }
                catch (COMException)
                {
                    Thread.Sleep(60);
                }
            }
            StatusText.Text = "The clipboard is busy; try Copy again.";
        }

        private static string ToRtf(string text, string fontName, bool symbolFont)
        {
            var rtf = new StringBuilder(@"{\rtf1\ansi\deff0{\fonttbl{\f0\fnil\fcharset")
                .Append(symbolFont ? 2 : 0)
                .Append(' ')
                .Append(EscapeRtf(fontName))
                .Append(@";}}\f0 ")
                .Append(EscapeRtf(text))
                .Append('}');
            return rtf.ToString();
        }

        private static string EscapeRtf(string text)
        {
            var sb = new StringBuilder(text.Length * 2);
            foreach (char ch in text)
            {
                if (ch is '\\' or '{' or '}')
                {
                    sb.Append('\\').Append(ch);
                }
                else if (ch == '\n')
                {
                    sb.Append(@"\par ");
                }
                else if (ch == '\r')
                {
                    continue;
                }
                else if (ch is >= ' ' and < '\x7F')
                {
                    sb.Append(ch);
                }
                else
                {
                    sb.Append(@"\u").Append((short)ch).Append('?');
                }
            }
            return sb.ToString();
        }

        private void SetSize(double points)
        {
            points = Math.Clamp(Math.Round(points, 1), MIN_SIZE, MAX_SIZE);
            m_sizePt = points;
            Chars.GlyphSize = points * 96 / 72;
            string text = points.ToString(CultureInfo.CurrentCulture);
            if (SizeCombo.Text != text)
            {
                SizeCombo.Text = text;
            }
        }

        private void CommitSizeText()
        {
            if (double.TryParse(SizeCombo.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out double points) && points > 0)
            {
                SetSize(points);
            }
            else
            {
                SizeCombo.Text = m_sizePt.ToString(CultureInfo.CurrentCulture);
            }
        }

        private void Zoom(int direction)
        {
            if (direction > 0)
            {
                SetSize(m_sizes.Where(s => s > m_sizePt).DefaultIfEmpty(m_sizePt * 1.25).First());
            }
            else if (direction < 0)
            {
                SetSize(m_sizes.Where(s => s < m_sizePt).DefaultIfEmpty(m_sizePt / 1.25).Last());
            }
        }

        private void WindowPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers != ModifierKeys.Control)
            {
                return;
            }

            switch (e.Key)
            {
                case Key.OemPlus or Key.Add:
                    Zoom(+1);
                    e.Handled = true;
                    break;
                case Key.OemMinus or Key.Subtract:
                    Zoom(-1);
                    e.Handled = true;
                    break;
                case Key.D0 or Key.NumPad0:
                    SetSize(DEFAULT_SIZE);
                    e.Handled = true;
                    break;
                case Key.C when Chars.IsKeyboardFocused && Chars.SelectedCode >= 0:
                    CopyToClipboard(char.ConvertFromUtf32(Chars.SelectedCode));
                    e.Handled = true;
                    break;
            }
        }

        private void RestorePlacement()
        {
            Width = Math.Max(m_settings.Width, MinWidth);
            Height = Math.Max(m_settings.Height, MinHeight);
            var screen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);

            if (m_settings.Left is double left && m_settings.Top is double top && screen.Contains(new Point(left + 60, top + 20)))
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = left;
                Top = top;
            }
            else
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            if (m_settings.Maximized)
            {
                WindowState = WindowState.Maximized;
            }
        }

        private void SaveSettings()
        {
            var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
            if (!bounds.IsEmpty)
            {
                m_settings.Left = bounds.Left;
                m_settings.Top = bounds.Top;
                m_settings.Width = bounds.Width;
                m_settings.Height = bounds.Height;
            }
            m_settings.Maximized = WindowState == WindowState.Maximized;
            m_settings.Font = (FontCombo.SelectedItem as FontEntry)?.Name ?? m_settings.Font;
            m_settings.Size = m_sizePt;
            m_settings.PreviewWidth = PreviewColumn.ActualWidth;
            m_settings.Advanced = AdvancedCheck.IsChecked == true;
            m_settings.Charset = (CharsetCombo.SelectedItem as CodePage)?.Id ?? 0;
            m_settings.Save();
        }

        #endregion

    }
}
