using System;
using System.Threading.Tasks;
using System.Windows;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using QRCoder;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;

namespace PhotoPopupReceiver
{
    public partial class MainWindow : Window
    {
        private readonly AppSettings _settings = new();
        private readonly PhotoReceiver _receiver = new();
        private PhotoPopupWindow? _popup;
        private TrayIconService? _tray;
        private bool _reallyClose = false;

        public MainWindow()
        {
            InitializeComponent();
            Closing += MainWindow_Closing;
            StateChanged += MainWindow_StateChanged;

            Loaded += async (_, __) =>
            {
                // UI init (optioneel: laat defaults zien)
                RequirePasswordCheckBox.IsChecked = _settings.RequirePassword;
                PasswordBox.Password = _settings.Password;
                ValidatePasswordUi();

                // ✅ BELANGRIJK: pak actuele waarden uit UI vóór server start
                _settings.RequirePassword = RequirePasswordCheckBox.IsChecked == true;
                _settings.Password = PasswordBox.Password;
                _settings.RequirePassword = true;
                _settings.Password = "test123";

                if (_settings.RequirePassword && string.IsNullOrWhiteSpace(_settings.Password))
                {
                    ValidatePasswordUi();
                    return; // niet starten als wachtwoord verplicht is maar leeg
                }

                var ip = GetLanIPv4() ?? "LAN-IP";
                var url = $"http://{ip}:{_settings.Port}/push-photo";
                UrlText.Text = $"Endpoint:\n{url}\n\nAuth: {(_settings.RequirePassword ? "wachtwoord vereist (X-Auth header)" : "open")}";

                await _receiver.StartAsync(_settings, OnPhotoSavedAsync);

                _tray = new TrayIconService(
                    onOpen: () => Dispatcher.Invoke(ShowFromTray),
                    onQuit: () => Dispatcher.Invoke(QuitFromTray)
                );
            };
        }
        private void RequirePasswordChanged(object sender, RoutedEventArgs e)
        {
            _settings.RequirePassword = RequirePasswordCheckBox.IsChecked == true;
            ValidatePasswordUi();
        }

        private void PasswordChanged(object sender, RoutedEventArgs e)
        {
            _settings.Password = PasswordBox.Password;
            ValidatePasswordUi();
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
                _tray?.ShowBalloon("PhotoPopupReceiver", "Draait nu in de tray.");
            }
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_reallyClose) return;

            // standaard gedrag: naar tray i.p.v. echt afsluiten
            e.Cancel = true;
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


        private Task OnPhotoSavedAsync(string savedPath)
        {
            // Popup vervangen
            if (_settings.AutoPopup)
            {
                Dispatcher.Invoke(() =>
                {
                    _popup?.Close();
                    _popup = new PhotoPopupWindow();
                    _popup.SetImage(savedPath);
                    _popup.Show();
                    _popup.Dispatcher.BeginInvoke(new Action(_popup.PositionBottomRight));
                });
            }

            // Optioneel auto-copy
            if (_settings.AutoCopyToClipboard)
            {
                Dispatcher.Invoke(() =>
                {
                    // hergebruik je bestaande Copy code (of roep ClipboardHelper aan als je die hebt)
                    ClipboardHelper.CopyImageFileToClipboard(savedPath);
                });
            }

            return Task.CompletedTask;
        }

        private static string? GetLanIPv4()
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                var ipProps = ni.GetIPProperties();
                var addr = ipProps.UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork
                                      && !IPAddress.IsLoopback(a.Address));
                if (addr != null) return addr.Address.ToString();
            }
            return null;
        }
        private BitmapSource MakeQrBitmap(string text)
        {
            using var gen = new QRCodeGenerator();
            using var data = gen.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
            using var code = new QRCode(data);
            using Bitmap bmp = code.GetGraphic(20);

            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            ms.Position = 0;

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = ms;
            image.EndInit();
            image.Freeze();
            return image;
        }
        private void ShowQr_Click(object sender, RoutedEventArgs e)
        {
            // Neem dezelfde URL die je ook in UrlText toont
            var ip = GetLanIPv4() ?? "LAN-IP";
            var url = $"http://{ip}:{_settings.Port}/push-photo?token={_settings.Token}";

            QrImage.Source = MakeQrBitmap(url);
            QrImage.Visibility = QrImage.Visibility == System.Windows.Visibility.Visible
                ? System.Windows.Visibility.Collapsed
                : System.Windows.Visibility.Visible;
        }

        private void AutoCopyChanged(object sender, RoutedEventArgs e)
        {
            _settings.AutoCopyToClipboard = AutoCopyCheckBox.IsChecked == true;
        }

    }
}
