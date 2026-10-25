using System.IO;
using System.Security;
using Microsoft.Win32;

namespace NullSave.CharPad
{

    /// <summary>
    /// Registers CharPad under the per-user App Paths key so typing "charpad" in the Run box (Win+R) or Start search
    /// launches it, without needing admin rights or a change to PATH.
    /// </summary>
    internal static class AppRegistration
    {

        #region Constants

        private const string APP_PATHS_KEY = @"Software\Microsoft\Windows\CurrentVersion\App Paths\CharPad.exe";

        #endregion

        #region Public Methods

        /// <summary>
        /// Points the App Paths entry at this executable, updating it if CharPad has moved. Skipped when running from a
        /// build output (bin) folder so development builds don't take over the registration.
        /// </summary>
        public static void Register()
        {
            string? exe = Environment.ProcessPath;
            if (exe == null || IsBuildOutput(exe))
            {
                return;
            }

            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(APP_PATHS_KEY);
                if (!string.Equals(key.GetValue(null) as string, exe, StringComparison.OrdinalIgnoreCase))
                {
                    key.SetValue(null, exe);
                }
            }
            catch (Exception e) when (e is UnauthorizedAccessException or SecurityException or IOException)
            { }
        }

        #endregion

        #region Private Methods

        private static bool IsBuildOutput(string exe)
        {
            string folder = Path.GetDirectoryName(exe) ?? "";
            return folder.Split(Path.DirectorySeparatorChar).Any(part => part.Equals("bin", StringComparison.OrdinalIgnoreCase));
        }

        #endregion

    }
}
