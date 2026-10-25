using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace NullSave.CharPad
{

    /// <summary>
    /// Character names and blocks come from the ICU library that ships with Windows 10 1903+ (icu.dll), so they cover
    /// all of Unicode rather than just the BMP. Categories come from .NET's own tables.
    /// </summary>
    internal static class UnicodeInfo
    {

        #region Imports

        [DllImport("icu.dll")]
        internal static extern int u_charName(int code, int nameChoice, byte[] buffer, int bufferLength, ref int errorCode);

        [DllImport("icu.dll")]
        internal static extern int ublock_getCode(int code);

        [DllImport("icu.dll")]
        internal static extern IntPtr u_getPropertyValueName(int property, int value, int nameChoice);

        #endregion

        #region Constants

        private const int UCHAR_BLOCK = 0x1001;
        private const int U_LONG_PROPERTY_NAME = 1;
        private const int U_UNICODE_CHAR_NAME = 0;
        private const int U_EXTENDED_CHAR_NAME = 2;
        private const int U_CHAR_NAME_ALIAS = 3;

        #endregion

        #region Fields

        private static readonly Dictionary<int, string> m_names = new();
        private static readonly Dictionary<int, string> m_blocks = new();
        private static readonly string[] m_categories = Enum.GetValues<UnicodeCategory>().Select(c => Spaced(c.ToString())).ToArray();
        private static bool m_icuAvailable = true;

        #endregion

        #region Public Methods

        /// <summary>
        /// The character's Unicode name (falling back to its alias, then ICU's label such as &lt;control-0009&gt;), with
        /// private-use code points reported as "PRIVATE USE". Empty if ICU isn't available.
        /// </summary>
        public static string Name(int code)
        {
            // Check cache first
            if (m_names.TryGetValue(code, out var name))
            {
                return name;
            }

            name = IcuName(code, U_UNICODE_CHAR_NAME);
            if (name.Length == 0) name = IcuName(code, U_CHAR_NAME_ALIAS);
            if (name.Length == 0) name = IcuName(code, U_EXTENDED_CHAR_NAME);
            if (name.StartsWith("<private", StringComparison.Ordinal))
            {
                name = "PRIVATE USE";
            }
            m_names[code] = name;

            return name;
        }

        /// <summary>The Unicode block containing the code point, e.g. "Latin 1 Supplement", or "Unknown" if ICU isn't available.</summary>
        public static string Block(int code)
        {
            if (!m_icuAvailable)
            {
                return "Unknown";
            }

            try
            {
                int block = ublock_getCode(code);
                if (!m_blocks.TryGetValue(block, out var name))
                {
                    name = Marshal.PtrToStringAnsi(u_getPropertyValueName(UCHAR_BLOCK, block, U_LONG_PROPERTY_NAME))?.Replace('_', ' ') ?? "Unknown";
                    m_blocks[block] = name;
                }
                return name;
            }
            catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
            {
                m_icuAvailable = false;
                return "Unknown";
            }
        }

        /// <summary>The code point's general category as readable text, e.g. "Uppercase Letter".</summary>
        public static string Category(int code)
        {
            return m_categories[(int)CharUnicodeInfo.GetUnicodeCategory(code)];
        }

        /// <summary>Whether the grid should offer this code point (charmap hides control codes too).</summary>
        public static bool IsDisplayable(int code)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(code);
            return category is not (UnicodeCategory.Control or UnicodeCategory.Surrogate);
        }

        #endregion

        #region Private Methods

        private static string IcuName(int code, int choice)
        {
            if (!m_icuAvailable)
            {
                return "";
            }

            try
            {
                var buffer = new byte[256];
                int error = 0;
                int length = u_charName(code, choice, buffer, buffer.Length, ref error);
                return error > 0 || length <= 0 ? "" : Encoding.ASCII.GetString(buffer, 0, Math.Min(length, buffer.Length));
            }
            catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
            {
                m_icuAvailable = false;
                return "";
            }
        }

        private static string Spaced(string pascal)
        {
            var sb = new StringBuilder(pascal.Length + 4);
            foreach (char ch in pascal)
            {
                if (char.IsUpper(ch) && sb.Length > 0)
                {
                    sb.Append(' ');
                }
                sb.Append(ch);
            }

            return sb.ToString();
        }

        #endregion

    }
}
