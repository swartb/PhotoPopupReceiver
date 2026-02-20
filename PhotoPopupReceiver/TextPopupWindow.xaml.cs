using System;
using System.Windows;

namespace PhotoPopupReceiver
{
    /// <summary>
    /// A borderless, always-on-top notification window that displays a received text message
    /// in the bottom-right corner of the screen.
    /// The text is selectable so the user can copy a portion of it manually.
    /// The window provides two action buttons: one to copy the full text to the clipboard
    /// and one to dismiss the popup.
    /// </summary>
    public partial class TextPopupWindow : Window
    {
        // The text message currently shown in the popup.
        private string _messageText = string.Empty;

        /// <summary>
        /// Initializes the window, applies the current localization, and subscribes to
        /// <see cref="LocalizationManager.LanguageChanged"/> so the button labels update
        /// automatically if the user switches language while the popup is open.
        /// The subscription is removed when the window closes to avoid memory leaks.
        /// </summary>
        public TextPopupWindow()
        {
            InitializeComponent();
            // PositionBottomRight relies on ActualWidth/ActualHeight, which are only
            // available after the first layout pass triggered by the Loaded event.
            Loaded += (_, __) => PositionBottomRight();

            // Apply button labels for the current language.
            ApplyLocalization();

            // Keep button labels in sync if the language is switched while the popup is open.
            LocalizationManager.LanguageChanged += OnLanguageChanged;
            Closed += (_, __) => LocalizationManager.LanguageChanged -= OnLanguageChanged;
        }

        // Handler that marshals the localization refresh onto the UI thread.
        private void OnLanguageChanged(object? sender, EventArgs e) =>
            Dispatcher.Invoke(ApplyLocalization);

        // Sets the Content of each button from the current resource strings.
        private void ApplyLocalization()
        {
            CopyButton.Content = LocalizationManager.GetString("CopyButton");
            CloseButton.Content = LocalizationManager.GetString("CloseButton");
        }

        /// <summary>
        /// Sets the text message to be displayed in the popup.
        /// </summary>
        /// <param name="message">The received text message to display.</param>
        public void SetText(string message)
        {
            _messageText = message ?? string.Empty;
            MessageTextBox.Text = _messageText;
        }

        /// <summary>
        /// Moves the window to the bottom-right corner of the primary monitor's working area,
        /// leaving a small margin so it does not overlap the taskbar or screen edges.
        /// This method should be called after the window's layout has been measured
        /// (i.e. <see cref="FrameworkElement.ActualWidth"/> and
        /// <see cref="FrameworkElement.ActualHeight"/> have non-zero values).
        /// </summary>
        public void PositionBottomRight()
        {
            var wa = SystemParameters.WorkArea;
            const double margin = 16;
            Left = wa.Right - ActualWidth - margin;
            Top = wa.Bottom - ActualHeight - margin;
        }

        /// <summary>
        /// Handles the "Copy" button click.
        /// Copies the full text of the received message to the Windows clipboard.
        /// </summary>
        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_messageText))
                System.Windows.Clipboard.SetText(_messageText);

            TrayIconService.Current?.ShowBalloon(
                LocalizationManager.GetString("CopiedToClipboardTitle"),
                LocalizationManager.GetString("CopiedToClipboardMessage"));

            Close();
        }

        /// <summary>
        /// Handles the "Close" button click. Dismisses the popup window.
        /// </summary>
        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
