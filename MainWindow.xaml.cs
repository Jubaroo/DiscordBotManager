using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Resources;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;
using WpfRichTextBox = System.Windows.Controls.RichTextBox;
using WpfFontFamily = System.Windows.Media.FontFamily;
using WpfBrush      = System.Windows.Media.Brush;
using System.Text.RegularExpressions;
using System.Windows.Documents;
using Brushes = System.Windows.Media.Brushes;
using System.Windows.Forms;           // for NotifyIcon, ToolTipIcon

namespace DiscordBotManager {
    public partial class MainWindow : Window {
        private ObservableCollection<Bot> bots = new ObservableCollection<Bot>();
        private NotifyIcon trayIcon;
        private bool appClosing = false;  // true when we are actually exiting the app

        private string configPath;  // path to config file in AppData

        public MainWindow() {
            InitializeComponent();
            // Data binding: attach bots collection to the ListBox
            BotList.ItemsSource = bots;

            // Prepare AppData config path
            string appDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DiscordBotManager");
            configPath = Path.Combine(appDir, "bots.json");
            Directory.CreateDirectory(appDir);

            // Load previously saved bot list (if any)
            LoadConfig();

            // Initialize system tray icon and menu
            InitTrayIcon();

            // Update "Start All"/"Stop All" button states initially
            UpdateGlobalButtons();
        }

        private void LoadConfig() {
            if (!File.Exists(configPath)) return;
            try {
                string json = File.ReadAllText(configPath);
                var paths = JsonSerializer.Deserialize<ObservableCollection<string>>(json);
                if (paths == null) return;
                foreach (string path in paths) {
                    if (!Directory.Exists(path)) continue; // skip missing directories
                    string name = Path.GetFileName(path);
                    Bot bot = new Bot { Name = name, Path = path, IsRunning = false };
                    AddBotToUI(bot);
                }
            } catch (Exception ex) {
                MessageBox.Show("Failed to load bot configuration:\n" + ex.Message, "Error");
            }
        }

        private void SaveConfig() {
            try {
                var paths = bots.Select(b => b.Path).ToList();
                string json = JsonSerializer.Serialize(paths, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configPath, json);
            } catch (Exception ex) {
                // Non-critical: just show a warning if save fails
                MessageBox.Show("Failed to save configuration:\n" + ex.Message, "Warning");
            }
        }

        private void InitTrayIcon() {
            // 1) Create and configure the NotifyIcon
            trayIcon = new NotifyIcon();
            var uri = new Uri("pack://application:,,,/app.ico", UriKind.Absolute);
            StreamResourceInfo info = System.Windows.Application.GetResourceStream(uri);
            trayIcon.Icon    = new Icon(info.Stream);
            trayIcon.Visible = true;
            trayIcon.Text    = "Discord Bot Manager";

            // 2) Build the context menu (Show/Hide + Exit)
            trayIcon.ContextMenuStrip = new ContextMenuStrip();
            var showHideItem = new ToolStripMenuItem("Show");
            var exitItem     = new ToolStripMenuItem("Exit");

            showHideItem.Click += (s, e) => {
                if (!IsVisible || WindowState == WindowState.Minimized)
                    ShowMainWindow();
                else
                    HideMainWindow();
            };
            exitItem.Click += (s, e) => {
                appClosing = true;
                Close();
            };

            trayIcon.ContextMenuStrip.Items.Add(showHideItem);
            trayIcon.ContextMenuStrip.Items.Add(exitItem);

            // Update the Show/Hide text dynamically
            trayIcon.ContextMenuStrip.Opening += (s, e) => {
                showHideItem.Text = (IsVisible && WindowState != WindowState.Minimized)
                    ? "Hide"
                    : "Show";
            };

            // 3) Double-click tray icon to restore
            trayIcon.DoubleClick += (s, e) => {
                Dispatcher.Invoke(() => ShowMainWindow());
            };

            // 4) Minimize to tray when window is minimized
            StateChanged += (s, e) => {
                if (WindowState == WindowState.Minimized && !appClosing) {
                    HideMainWindow();
                }
            };

            // 5) Intercept the “X” (Close) button
            Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            // Only intercept if we haven’t already chosen to exit
            if (!appClosing)
            {
                // 1) Prompt the user
                var result = MessageBox.Show(
                    "Do you want to keep Discord Bot Manager running in the system tray when you click Close?\n\n" +
                    "Yes → Minimize to tray\n" +
                    "No  → Exit application",
                    "Minimize to Tray?",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question,
                    MessageBoxResult.Yes
                );

                // 2a) If they want “Yes”, cancel shutdown and hide to tray
                if (result == MessageBoxResult.Yes)
                {
                    e.Cancel = true;
                    HideMainWindow();    // your existing method that hides & shows shell balloon
                }
                else
                {
                    // 2b) They want “No” → actually exit
                    appClosing = true;

                    // Stop all bots
                    foreach (var bot in bots)
                    {
                        if (bot.IsRunning && bot.Process != null && !bot.Process.HasExited)
                        {
                            try { bot.Process.Kill(); }
                            catch { /* ignore */ }
                        }
                    }

                    // Dispose the tray icon so it disappears immediately
                    trayIcon.Dispose();

                    // Allow close to proceed
                    e.Cancel = false;
                }
            }
            // If appClosing was already true, we fall through and let WPF close normally
        }

        private void ShowMainWindow() {
            this.ShowInTaskbar = true;
            this.Show();
            if (this.WindowState == WindowState.Minimized) {
                this.WindowState = WindowState.Normal;
            }
            this.Activate();
        }

        private void HideMainWindow() {
            // 1) actually hide the window
            this.ShowInTaskbar = false;
            this.Hide();

            // 2) show a balloon tip so the user knows we're still running
            trayIcon.BalloonTipTitle = "Discord Bot Manager";
            trayIcon.BalloonTipText  = "App minimized to tray. Double-click the icon to restore.";
            trayIcon.BalloonTipIcon  = ToolTipIcon.Info;
            trayIcon.ShowBalloonTip(3000);  // duration in ms
        }

        // Update the enabled/disabled state of "Start All" / "Stop All" buttons
        private void UpdateGlobalButtons() {
            StartAllButton.IsEnabled = bots.Any(b => !b.IsRunning);
            StopAllButton.IsEnabled  = bots.Any(b =>  b.IsRunning);
        }

        // Add a new bot to the UI (list and logs)
        private void AddBotToUI(Bot bot) {
            // Create a new tab for this bot's logs
            var logTextBox = new WpfRichTextBox {
                AcceptsReturn = true,
                AcceptsTab    = true,
                VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                IsReadOnly    = true,
                FontFamily    = new WpfFontFamily("Consolas"),
                Background    = (WpfBrush)FindResource("AppBackgroundBrush"),
                Foreground    = (WpfBrush)FindResource("AppForegroundBrush"),
                Margin        = new Thickness(5)
            };
            logTextBox.Document = new FlowDocument();
            bot.LogTextBox = logTextBox;
            var tab = new TabItem {
                Header = bot.Name,
                Content = logTextBox,
                DataContext = bot
            };
            bot.LogTextBox = logTextBox;
            bot.LogTab = tab;
            bots.Add(bot);
            LogTabs.Items.Add(tab);
        }

        // ANSI‐parsing helper for colored console output
        private static readonly Regex AnsiRegex = new Regex(@"\x1B\[(\d+)m", RegexOptions.Compiled);

        private void AppendAnsiText(WpfRichTextBox box, string text) {
            var doc = box.Document;
            // Try to reuse the last block if it's already a Paragraph
            Paragraph paragraph = doc.Blocks.LastBlock as Paragraph;
            if (paragraph == null) {
                // Otherwise create one and add it
                paragraph = new Paragraph();
                doc.Blocks.Add(paragraph);
            }
            int lastIndex = 0;
            // default brush from your theme
            WpfBrush currentBrush = FindResource("AppForegroundBrush") as WpfBrush ?? throw new InvalidOperationException();

            foreach (Match m in AnsiRegex.Matches(text)) {
                // text before this ANSI code
                if (m.Index > lastIndex) {
                    paragraph.Inlines.Add(
                        new Run(text.Substring(lastIndex, m.Index - lastIndex)) {
                            Foreground = currentBrush
                        });
                }
                // update brush based on code
                int code = int.Parse(m.Groups[1].Value);
                switch (code) {
                    case 30: currentBrush = Brushes.Black;     break;
                    case 31: currentBrush = Brushes.Red;       break;  // errors
                    case 32: currentBrush = Brushes.Green;     break;  // info
                    case 33: currentBrush = Brushes.Goldenrod; break;  // warnings
                    case 34: currentBrush = Brushes.Blue;      break;  // debug / namespaces
                    case 35: currentBrush = Brushes.Magenta;   break;  // “purple” logs
                    case 36: currentBrush = Brushes.Cyan;      break;  // verbose
                    case 37: currentBrush = (WpfBrush)FindResource("AppForegroundBrush"); break; // normal
                    case 0:  currentBrush = (WpfBrush)FindResource("AppForegroundBrush"); break; // reset
                }
                lastIndex = m.Index + m.Length;
            }
            // any trailing text
            if (lastIndex < text.Length) {
                paragraph.Inlines.Add(
                    new Run(text.Substring(lastIndex)) { Foreground = currentBrush });
            }
            box.ScrollToEnd();
        }

        // Launch the bot's Node.js process and start logging
        private void StartBot(Bot bot) {
            // Only start if not already running
            if (bot.IsRunning || bot.Process != null) return;

            // Determine how to launch the bot: prefer index.js, else npm start
            string indexPath = Path.Combine(bot.Path, "index.js");
            ProcessStartInfo psi;

            if (File.Exists(indexPath)) {
                // Explicitly run index.js
                psi = new ProcessStartInfo("node", "index.js") {
                    WorkingDirectory       = bot.Path,
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true
                };
            }
            else if (File.Exists(Path.Combine(bot.Path, "package.json"))) {
                // Fall back to npm start if package.json is present
                psi = new ProcessStartInfo("npm", "start") {
                    WorkingDirectory       = bot.Path,
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true
                };
            }
            else {
                // Neither index.js nor package.json found
                bot.LogTextBox.AppendText($"[ERROR] No index.js or package.json in {bot.Path}\n");
                return;
            }

            // Create and configure the process
            Process proc = new Process { StartInfo = psi, EnableRaisingEvents = true };

            // Attach stdout handler
            proc.OutputDataReceived += (s, e) =>
            {
                if (e.Data == null) return;
                Dispatcher.Invoke(() =>
                    AppendAnsiText(bot.LogTextBox, e.Data + Environment.NewLine)
                );
            };

            // Attach stderr handler
            proc.ErrorDataReceived += (s, e) =>
            {
                if (e.Data == null) return;
                Dispatcher.Invoke(() =>
                    AppendAnsiText(bot.LogTextBox, "[ERROR] " + e.Data + Environment.NewLine)
                );
            };

            // Attach exit/restart handler
            proc.Exited += (s, e) =>
            {
                Dispatcher.BeginInvoke((Action)(() =>
                {
                    if (appClosing) return;

                    if (bot.isStopping)
                    {
                        // User requested stop
                        bot.isStopping = false;
                        bot.IsRunning = false;
                        AppendAnsiText(bot.LogTextBox, "[INFO] Bot process stopped by user." + Environment.NewLine);
                    }
                    else
                    {
                        // Unexpected crash → restart
                        int code = proc.ExitCode;
                        bot.IsRunning = false;
                        AppendAnsiText(bot.LogTextBox, $"[ERROR] Bot process exited (code {code}). Restarting...\n");
                        bot.LogTextBox.ScrollToEnd();

                        // 1) Clean up old process
                        proc.Dispose();
                        bot.Process = null;
                        UpdateGlobalButtons();

                        // 2) Now actually restart
                        StartBot(bot);

                        return; // exit early since we've already disposed
                    }

                    // Common cleanup after a user-initiated stop
                    proc.Dispose();
                    bot.Process = null;
                    UpdateGlobalButtons();
                }));
            };

            // Start the process
            try {
                proc.Start();
            }
            catch (Exception ex) {
                bot.LogTextBox.AppendText("[ERROR] Failed to start bot process: " + ex.Message + "\n");
                return;
            }

            // Begin reading streams
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            bot.Process    = proc;
            bot.isStopping = false;
            bot.IsRunning  = true;

            // Log and update UI
            AppendAnsiText(bot.LogTextBox, "[INFO] Bot process started." + Environment.NewLine);
            bot.LogTextBox.ScrollToEnd();
            UpdateGlobalButtons();
        }

        private void AddBot_Click(object sender, RoutedEventArgs e)
        {
            using var folderDialog = new FolderBrowserDialog {
                Description = "Select the Discord bot folder"
            };
            if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                string folder = folderDialog.SelectedPath;
                // Prevent duplicates
                if (bots.Any(b => 
                        string.Equals(b.Path, folder, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show("This bot is already added.", "Notice");
                    return;
                }
                // create and add
                string botName = Path.GetFileName(folder);
                var newBot = new Bot { Name = botName, Path = folder, IsRunning = false };
                AddBotToUI(newBot);
                SaveConfig();
                UpdateGlobalButtons();
            }
        }

        // ←— Click="StartAll_Click" in your XAML
        private void StartAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var bot in bots) {
                if (!bot.IsRunning)
                    StartBot(bot);
            }
        }

        private void StopAll_Click(object sender, RoutedEventArgs e)
        {
            // Capture list to avoid collection-modified issues
            var botsToStop = bots.Where(b => b.IsRunning && b.Process != null).ToList();

            Task.Run(() =>
            {
                foreach (var bot in botsToStop)
                {
                    bot.isStopping = true;
                    try
                    {
                        bot.Process.Kill();
                    }
                    catch
                    {
                        // ignore if already exited
                    }
                }
            });
        }

        private void BotStartStop_Click(object sender, RoutedEventArgs e) {
            if (sender is Button btn && btn.DataContext is Bot bot) {
                if (bot.IsRunning) {
                    // Prevent double-stop clicks
                    if (!bot.isStopping) {
                        bot.isStopping = true;
                        try { bot.Process?.Kill(); } catch { /* ignore */ }
                    }
                } else {
                    StartBot(bot);
                }
            }
        }

        // Restart a single bot: if it's running, kill without setting isStopping,
        // so the Exited event will auto-restart it; if it's stopped, just Start it.
        private void BotRestart_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Bot bot)
            {
                if (bot.Process != null && !bot.Process.HasExited)
                {
                    // don't set bot.isStopping => Exited handler will call StartBot(bot)
                    try { bot.Process.Kill(); }
                    catch { /* ignore */ }
                }
                else
                {
                    // if not running, just start
                    StartBot(bot);
                }
            }
        }

        // Restart all bots: for each running bot, kill it (will auto-restart);
        // for each stopped bot, call StartBot immediately.
        private void RestartAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var bot in bots)
            {
                if (bot.Process != null && !bot.Process.HasExited)
                {
                    try { bot.Process.Kill(); }
                    catch { /* ignore */ }
                }
                else
                {
                    StartBot(bot);
                }
            }
        }

        private void BotRemove_Click(object sender, RoutedEventArgs e) {
            if (sender is Button btn && btn.DataContext is Bot bot) {
                // 1. Ask for confirmation
                var message   = $"Are you sure you want to remove bot \"{bot.Name}\"?";
                var caption   = "Confirm Remove Bot";
                var buttons   = MessageBoxButton.YesNo;
                var icon      = MessageBoxImage.Warning;
                var result    = MessageBox.Show(message, caption, buttons, icon);

                // 2. Only proceed if they clicked “Yes”
                if (result != MessageBoxResult.Yes)
                    return;
                // Only remove if not running
                if (bot.IsRunning) {
                    MessageBox.Show("Stop the bot before removing it.", "Cannot Remove");
                    return;
                }
                // Remove from UI list and tabs
                bots.Remove(bot);
                if (bot.LogTab != null) {
                    LogTabs.Items.Remove(bot.LogTab);
                }
                SaveConfig();
                UpdateGlobalButtons();
            }
        }
    }
}