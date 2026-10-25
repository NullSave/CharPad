using System.Globalization;
using System.Text;

namespace NullSave.CharPad
{

    /// <summary>The "Character set" choices from charmap's advanced view, and the set of characters each one can encode.</summary>
    internal static class CodePages
    {

        #region Fields

        public static readonly CodePage[] All =
        [
            new(0, "Unicode"),
            new(1252, "Windows: Western"),
            new(1250, "Windows: Central European"),
            new(1251, "Windows: Cyrillic"),
            new(1253, "Windows: Greek"),
            new(1254, "Windows: Turkish"),
            new(1255, "Windows: Hebrew"),
            new(1256, "Windows: Arabic"),
            new(1257, "Windows: Baltic"),
            new(1258, "Windows: Vietnamese"),
            new(874, "Windows: Thai"),
            new(932, "Windows: Japanese Shift-JIS"),
            new(936, "Windows: Simplified Chinese GBK"),
            new(949, "Windows: Korean"),
            new(950, "Windows: Traditional Chinese Big5"),
            new(437, "DOS: United States"),
            new(737, "DOS: Greek"),
            new(775, "DOS: Baltic"),
            new(850, "DOS: Western Europe"),
            new(852, "DOS: Central Europe"),
            new(855, "DOS: Cyrillic"),
            new(857, "DOS: Turkish"),
            new(860, "DOS: Portuguese"),
            new(861, "DOS: Icelandic"),
            new(862, "DOS: Hebrew"),
            new(863, "DOS: French Canada"),
            new(864, "DOS: Arabic"),
            new(865, "DOS: Nordic"),
            new(866, "DOS: Russian"),
            new(869, "DOS: Modern Greek"),
        ];

        private static readonly Dictionary<int, HashSet<int>> m_cache = new();
        private static Dictionary<int, int>? m_ansiBytes;

        #endregion

        #region Public Methods

        /// <summary>Every code point the code page can represent, found by decoding each possible byte sequence.</summary>
        public static HashSet<int> Chars(int codePage)
        {
            // Check cache first
            if (m_cache.TryGetValue(codePage, out var set))
            {
                return set;
            }

            // Create new set
            set = new HashSet<int>();
            var encoding = GetEncoding(codePage);
            if (encoding != null)
            {
                var one = new byte[1];
                var two = new byte[2];
                for (int b1 = 0; b1 < 256; b1++)
                {
                    one[0] = (byte)b1;
                    if (TryDecode(encoding, one, out int code))
                    {
                        set.Add(code);
                        continue;
                    }
                    if (encoding.IsSingleByte)
                    {
                        continue;
                    }

                    // Not a character on its own, so it may be a DBCS lead byte.
                    two[0] = (byte)b1;
                    for (int b2 = 0x40; b2 < 0xFF; b2++)
                    {
                        two[1] = (byte)b2;
                        if (TryDecode(encoding, two, out code))
                        {
                            set.Add(code);
                        }
                    }
                }
            }

            m_cache[codePage] = set;

            return set;
        }

        /// <summary>The Alt+ number-pad keystroke that types the character, as charmap shows in its status bar.</summary>
        public static string Keystroke(int code)
        {
            if (code == 0x20)
            {
                return "Spacebar";
            }
            if (code is > 0x20 and < 0x7F)
            {
                return ((char)code).ToString();
            }

            m_ansiBytes ??= BuildAnsiBytes();
            return m_ansiBytes.TryGetValue(code, out int b) ? $"Alt+0{b}" : "";
        }

        #endregion

        #region Internal Methods

        internal static Dictionary<int, int> BuildAnsiBytes()
        {
            var map = new Dictionary<int, int>();
            var encoding = GetEncoding(CultureInfo.CurrentCulture.TextInfo.ANSICodePage);

            if (encoding is { IsSingleByte: true })
            {
                var one = new byte[1];
                for (int b = 0x80; b < 0x100; b++)
                {
                    one[0] = (byte)b;
                    if (TryDecode(encoding, one, out int code) && !map.ContainsKey(code))
                    {
                        map[code] = b;
                    }
                }
            }

            return map;
        }

        internal static Encoding? GetEncoding(int codePage)
        {
            try
            {
                return Encoding.GetEncoding(codePage, EncoderFallback.ReplacementFallback, new DecoderReplacementFallback("�"));
            }
            catch (Exception e) when (e is ArgumentException or NotSupportedException)
            {
                return null;
            }
        }

        internal static bool TryDecode(Encoding encoding, byte[] bytes, out int code)
        {
            string s = encoding.GetString(bytes);
            code = -1;
            if (s.Length == 1 && s[0] != '�' && !char.IsSurrogate(s[0]))
            {
                code = s[0];
            }
            else if (s.Length == 2 && char.IsSurrogatePair(s[0], s[1]))
            {
                code = char.ConvertToUtf32(s[0], s[1]);
            }

            return code >= 0;
        }

        #endregion

    }
}
