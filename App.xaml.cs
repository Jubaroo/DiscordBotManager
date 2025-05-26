using System.Windows;               // brings in StartupEventArgs, etc.
using System.Windows.Media;         // brings in SolidColorBrush, Colors
using Microsoft.Win32;              // brings in RegistryKey

namespace DiscordBotManager
{
    public partial class App : System.Windows.Application    // <-- fully-qualified
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            bool isLight = true;
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key?.GetValue("AppsUseLightTheme") is int v)
                    isLight = v > 0;
            }
            catch
            {
                // ignored
            }

            if (!isLight)
            {
                Resources["AppBackgroundBrush"]     = new SolidColorBrush(System.Windows.Media.Color.FromRgb(32,32,32));  // <-- fully-qualified
                Resources["AppForegroundBrush"]     = new SolidColorBrush(Colors.White);
                Resources["ControlBackgroundBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(58,58,58));  // <-- fully-qualified
                Resources["ControlForegroundBrush"] = new SolidColorBrush(Colors.White);
            }

            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();
        }
    }
}