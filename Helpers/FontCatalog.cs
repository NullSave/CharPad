using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;

namespace NullSave.CharPad
{

    internal static class FontCatalog
    {

        #region Public Methods

        /// <summary>Installed font families (machine-wide and per-user), sorted by name, excluding WPF's composite fonts.</summary>
        public static List<FontEntry> Load()
        {
            var english = XmlLanguage.GetLanguage("en-us");
            IEnumerable<FontFamily> families = Fonts.SystemFontFamilies;

            string userFonts = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Fonts");
            if (Directory.Exists(userFonts))
            {
                try
                {

                    families = families.Concat(Fonts.GetFontFamilies(new Uri(userFonts + Path.DirectorySeparatorChar)));
                }
                catch (Exception e) when (e is IOException or UriFormatException or ArgumentException)
                { }
            }

            var byName = new Dictionary<string, FontEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var family in families)
            {
                // "Global User Interface" and friends are composite fonts with no glyph table of their own.
                if (family.FamilyMaps.Count > 0)
                {
                    continue;
                }
                string name = family.FamilyNames.TryGetValue(english, out var n) ? n : family.FamilyNames.Values.FirstOrDefault() ?? family.Source;
                byName.TryAdd(name, new FontEntry(name, family));
            }
            
            return byName.Values.OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        /// <summary>The glyph table for the family's regular face, or null if WPF can't load one.</summary>
        public static GlyphTypeface? GetGlyphs(FontFamily family)
        {
            var typeface = new Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            return typeface.TryGetGlyphTypeface(out var glyphs) ? glyphs : null;
        }

        #endregion

    }
}
