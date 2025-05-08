using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Controls;
using WpfRichTextBox = System.Windows.Controls.RichTextBox;

namespace DiscordBotManager {
    public class Bot : INotifyPropertyChanged {
        public required string Name { get; set; }
        public required string Path { get; set; }

        private bool _isRunning;
        public bool IsRunning {
            get => _isRunning;
            set {
                if (_isRunning != value) {
                    _isRunning = value;
                    OnPropertyChanged(nameof(IsRunning));
                }
            }
        }

        // Process and UI references (not exposed for binding)
        public Process Process { get; set; }
        public bool isStopping { get; set; }    // flag to prevent auto-restart on manual stop
        public TabItem LogTab { get; set; }
        public WpfRichTextBox LogTextBox { get; set; }
        
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) {
            PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}