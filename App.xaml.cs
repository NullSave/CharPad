using System.Text;
using System.Windows;

namespace NullSave.CharPad;

public partial class App : Application
{
    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        // Makes the legacy Windows/DOS code pages available for the "Character set" filter and Alt+ keystrokes.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        // This line automatically registers the application path so you can run "charpad" from the Run prompt
        // Comment out if you don't want it.
        AppRegistration.Register();

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(args.Exception.Message, "CharPad", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        base.OnStartup(e);
    }
}
