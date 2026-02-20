using System;
using System.Threading.Tasks;
using System.Windows;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows.Controls;
using System.Windows.Threading;

namespace PhotoPopupReceiver
{
    /// <summary>
    /// The application's primary window.
    /// Displays the listening endpoint URL,
    /// and a checkbox that controls whether received photos are automatically copied to
    /// the clipboard.  On startup it launches the HTTP listener via <see cref="PhotoReceiver"/>
    /// and wires up the callback that handles each received photo.
    /// A language ComboBox allows the user to switch the UI between English and Dutch at runtime.
    /// </summary>
    public partial class MainWindow : Window
    {
        // Application settings shared between the UI and the HTTP listener.
        private readonly AppSettings _settings = AppSettings.Load();

        // The HTTP listener that accepts incoming photo upload requests.
        private readonly PhotoReceiver _receiver = new();

        // The currently open popup notification window, or null if none is shown.
        private PhotoPopupWindow? _popup;
        private TextPopupWindow? _textPopup;
        private TrayIconService? _tray;
        private bool _reallyClose = false;
        private bool _suppressMinimizeBalloon = false;
        private bool _isInitializing = true;
        private readonly DispatcherTimer _saveSettingsTimer;

        // The endpoint URL built at startup, stored separately so it can be
        // reformatted with a different language prefix when the language changes.
        private string _endpointUrl = string.Empty;

        /// <summary>
        /// Initializes the window, sets the correct language in the ComboBox, and once the
        /// window is fully loaded resolves the LAN IP address,
        /// and starts the HTTP listener.
        /// </summary>
        public MainWindow()
        {
            _saveSettingsTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _saveSettingsTimer.Tick += (_, __) =>
            {
                _saveSettingsTimer.Stop();
                _settings.Save();
            };

            InitializeComponent();
            Closing += MainWindow_Closing;
            StateChanged += MainWindow_StateChanged;

            // Populate language ComboBox before applying localization.
            LanguageComboBox.Items.Add(new ComboBoxItem { Content = "English", Tag = "en" });
            LanguageComboBox.Items.Add(new ComboBoxItem { Content = "Nederlands", Tag = "nl" });

            // Pre-select the item that matches the current language without firing the handler.
            foreach (ComboBoxItem item in LanguageComboBox.Items)
            {
                if (item.Tag is string tag && tag == LocalizationManager.CurrentCulture.TwoLetterISOLanguageName)
                {
                    LanguageComboBox.SelectedItem = item;
                    break;
                }
            }

            // Apply initial localization to all named controls.
            ApplyLocalization();

            // Keep the UI in sync whenever the language is changed from elsewhere.
            LocalizationManager.LanguageChanged += (_, __) => Dispatcher.Invoke(ApplyLocalization);

            _tray = new TrayIconService(
                onOpen: () => Dispatcher.Invoke(ShowFromTray),
                onQuit: () => Dispatcher.Invoke(QuitFromTray)
            );

            Loaded += async (_, __) =>
            {
                _isInitializing = true;

                if (!_settings.RequirePassword)
                    _settings.Password = string.Empty;

                // UI init → settings sync
                AutoCopyCheckBox.IsChecked = _settings.AutoCopyToClipboard;
                RequirePasswordCheckBox.IsChecked = _settings.RequirePassword;
                PasswordBox.Password = _settings.Password ?? "";
                PasswordTextBox.Text = PasswordBox.Password;
                ShowPasswordToggle.IsChecked = false;
                UpdatePasswordVisibilityUi();
                UpdatePasswordEnabledUi();
                ValidatePasswordUi();

                // Pak actuele waarden uit UI vóór server start
                _settings.AutoCopyToClipboard = AutoCopyCheckBox.IsChecked == true;
                _settings.RequirePassword = RequirePasswordCheckBox.IsChecked == true;
                _settings.Password = PasswordBox.Password;

                if (_settings.RequirePassword && string.IsNullOrWhiteSpace(_settings.Password))
                {
                    // Laat de app wel starten, maar forceer dat wachtwoord uit staat als er geen wachtwoord is.
                    _settings.RequirePassword = false;
                    RequirePasswordCheckBox.IsChecked = false;
                    ValidatePasswordUi();
                }

                var ip = GetLanIPv4() ?? "LAN-IP";
                _endpointUrl = $"http://{ip}:{_settings.Port}";

                ApplyLocalization();

                _isInitializing = false;

                _settings.Save();

                try
                {
                    await _receiver.StartAsync(_settings, OnPhotoSavedAsync, OnTextReceivedAsync);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(ex.ToString(), "StartAsync failed");
                }
            };
        }

        private void ScheduleSaveSettings()
        {
            if (_isInitializing)
                return;

            _saveSettingsTimer.Stop();
            _saveSettingsTimer.Start();
        }

        private void RequirePasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            var requirePassword = RequirePasswordCheckBox.IsChecked == true;
            _settings.RequirePassword = requirePassword;

            if (!requirePassword)
            {
                var prevInitializing = _isInitializing;
                _isInitializing = true;

                _settings.Password = string.Empty;
                PasswordBox.Password = string.Empty;
                PasswordTextBox.Text = string.Empty;
                ShowPasswordToggle.IsChecked = false;
                UpdatePasswordVisibilityUi();

                _isInitializing = prevInitializing;
            }

            UpdatePasswordEnabledUi();
            ValidatePasswordUi();
            ApplyLocalization();
            ScheduleSaveSettings();
        }

        private void PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            _settings.Password = PasswordBox.Password;
            if (PasswordTextBox.Visibility == Visibility.Visible && PasswordTextBox.Text != PasswordBox.Password)
                PasswordTextBox.Text = PasswordBox.Password;
            ValidatePasswordUi();
            ScheduleSaveSettings();
        }

        private void PasswordTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing) return;
            _settings.Password = PasswordTextBox.Text;
            if (PasswordBox.Visibility == Visibility.Visible && PasswordBox.Password != PasswordTextBox.Text)
                PasswordBox.Password = PasswordTextBox.Text;
            ValidatePasswordUi();
            ScheduleSaveSettings();
        }

        private void ShowPasswordToggle_Checked(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            UpdatePasswordVisibilityUi();
        }

        private void ShowPasswordToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            UpdatePasswordVisibilityUi();
        }

        private void UpdatePasswordVisibilityUi()
        {
            if (ShowPasswordToggle.IsChecked == true)
            {
                PasswordTextBox.Text = PasswordBox.Password;
                PasswordTextBox.Visibility = Visibility.Visible;
                PasswordBox.Visibility = Visibility.Collapsed;
                ShowPasswordToggle.Content = "\uE8A7"; // Hide
                ShowPasswordToggle.ToolTip = LocalizationManager.GetString("HidePasswordTooltip");
            }
            else
            {
                PasswordBox.Password = PasswordTextBox.Text;
                PasswordBox.Visibility = Visibility.Visible;
                PasswordTextBox.Visibility = Visibility.Collapsed;
                ShowPasswordToggle.Content = "\uE890"; // View
                ShowPasswordToggle.ToolTip = LocalizationManager.GetString("ShowPasswordTooltip");
            }
        }

        private void UpdatePasswordEnabledUi()
        {
            var enabled = _settings.RequirePassword;
            PasswordLabelText.IsEnabled = enabled;
            PasswordBox.IsEnabled = enabled;
            PasswordTextBox.IsEnabled = enabled;
            ShowPasswordToggle.IsEnabled = enabled;

            if (!enabled)
            {
                ShowPasswordToggle.IsChecked = false;
                UpdatePasswordVisibilityUi();
            }
        }

        private void ValidatePasswordUi()
        {
            if (PasswordErrorText == null) return; // UI nog niet klaar

            bool needs = _settings.RequirePassword;
            bool ok = !needs || !string.IsNullOrWhiteSpace(_settings.Password);

            PasswordErrorText.Visibility = ok ? Visibility.Collapsed : Visibility.Visible;
        }
        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
                if (_suppressMinimizeBalloon)
                {
                    _suppressMinimizeBalloon = false;
                    return;
                }

                _tray?.ShowBalloon("PhotoPopupReceiver", "Draait nu in de tray.");
            }
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_reallyClose) return;

            // standaard gedrag: naar tray i.p.v. echt afsluiten
            e.Cancel = true;
            _suppressMinimizeBalloon = true;
            WindowState = WindowState.Minimized;
            Hide();
            _tray?.ShowBalloon("PhotoPopupReceiver", "Draait nu in de tray.");
        }

        private void ShowFromTray()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private void QuitFromTray()
        {
            _reallyClose = true;
            _tray?.Dispose();
            Close(); // triggert Closing event, maar we laten ‘m nu door
        }


        /// <summary>
        /// Refreshes all localised text in the window using the current culture from
        /// <see cref="LocalizationManager"/>.  Called once at startup and again whenever
        /// the user selects a different language.
        /// </summary>
        private void ApplyLocalization()
        {
            AppTitleText.Text = LocalizationManager.GetString("AppTitle");
            AutoCopyCheckBox.Content = LocalizationManager.GetString("AutoCopyCheckbox");
            LanguageLabelText.Text = LocalizationManager.GetString("LanguageLabel");
            EndpointGroupBox.Header = LocalizationManager.GetString("EndpointGroupTitle");
            OptionsGroupBox.Header = LocalizationManager.GetString("OptionsGroupTitle");
            SecurityGroupBox.Header = LocalizationManager.GetString("SecurityGroupTitle");
            RequirePasswordCheckBox.Content = LocalizationManager.GetString("RequirePasswordCheckbox");
            PasswordLabelText.Text = LocalizationManager.GetString("PasswordLabel");
            PasswordErrorText.Text = LocalizationManager.GetString("PasswordRequiredError");
            ShowPasswordToggle.ToolTip = LocalizationManager.GetString(ShowPasswordToggle.IsChecked == true
                ? "HidePasswordTooltip"
                : "ShowPasswordTooltip");

            if (!string.IsNullOrEmpty(_endpointUrl))
            {
                Title = string.Format(LocalizationManager.GetString("TitleBarListening"), _settings.Port);
                var authValue = _settings.RequirePassword
                    ? LocalizationManager.GetString("AuthRequired")
                    : LocalizationManager.GetString("AuthOpen");

                UrlText.Text = $"{LocalizationManager.GetString("EndpointLabel")}\n{_endpointUrl}/push-photo\n{_endpointUrl}/push-text\n\n" +
                               $"{LocalizationManager.GetString("AuthLabel")} {authValue}";
            }
        }

        /// <summary>
        /// Callback invoked by <see cref="PhotoReceiver"/> after a text message has been received.
        /// Shows a popup window with the received text if auto-popup is enabled.
        /// </summary>
        /// <param name="text">The received text message.</param>
        /// <returns>A completed <see cref="Task"/>.</returns>
        private Task OnTextReceivedAsync(string text)
        {
            if (_settings.AutoPopup)
            {
                Dispatcher.Invoke(() =>
                {
                    // Close the previous text popup before opening a new one to avoid stacking.
                    _textPopup?.Close();
                    _textPopup = new TextPopupWindow();
                    _textPopup.SetText(text);
                    _textPopup.Show();
                    // Position after Show() so ActualWidth/ActualHeight are available.
                    _textPopup.Dispatcher.BeginInvoke(new Action(_textPopup.PositionBottomRight));
                });
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles the language ComboBox selection change.
        /// Reads the <c>Tag</c> of the selected item (a two-letter ISO language code) and
        /// passes it to <see cref="LocalizationManager.SetLanguage"/>, which fires
        /// <see cref="LocalizationManager.LanguageChanged"/> so all subscribers update.
        /// </summary>
        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageComboBox.SelectedItem is ComboBoxItem item && item.Tag is string languageCode)
                LocalizationManager.SetLanguage(languageCode);
        }

        /// <summary>
        /// Callback invoked by <see cref="PhotoReceiver"/> after a photo has been saved to disk.
        /// Depending on the current settings, shows a popup window and/or copies the image
        /// to the clipboard.
        /// </summary>
        /// <param name="savedPath">The full path to the file that was saved.</param>
        /// <returns>A completed <see cref="Task"/>.</returns>
        private Task OnPhotoSavedAsync(string savedPath)
        {
            // Show (or replace) the popup notification window in the bottom-right corner.
            if (_settings.AutoPopup)
            {
                Dispatcher.Invoke(() =>
                {
                    // Close the previous popup before opening a new one to avoid stacking.
                    _popup?.Close();
                    _popup = new PhotoPopupWindow();
                    _popup.SetImage(savedPath);
                    _popup.Show();
                    // Position after Show() so ActualWidth/ActualHeight are available.
                    _popup.Dispatcher.BeginInvoke(new Action(_popup.PositionBottomRight));
                });
            }

            // Optionally place the image on the clipboard for immediate use.
            if (_settings.AutoCopyToClipboard)
            {
                Dispatcher.Invoke(() =>
                {
                    ClipboardHelper.CopyImageFileToClipboard(savedPath);
                });
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Returns the first active, non-loopback IPv4 address found on this machine,
        /// which is used to construct the endpoint URL that remote senders should call.
        /// Returns <see langword="null"/> if no suitable address is found.
        /// </summary>
        private static string? GetLanIPv4()
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                // Skip interfaces that are down or inactive.
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                // Skip the loopback adapter (127.0.0.1).
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                var ipProps = ni.GetIPProperties();
                var addr = ipProps.UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork
                                      && !IPAddress.IsLoopback(a.Address));
                if (addr != null) return addr.Address.ToString();
            }
            return null;
        }

        /// <summary>
        /// Handles the AutoCopy checkbox being checked or unchecked.
        /// Synchronises the UI state with <see cref="AppSettings.AutoCopyToClipboard"/>.
        /// </summary>
        private void AutoCopyChanged(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            _settings.AutoCopyToClipboard = AutoCopyCheckBox.IsChecked == true;
            ScheduleSaveSettings();
        }

    }
}
