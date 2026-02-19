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

        public MainWindow()
        {
            InitializeComponent();

            Loaded += async (_, __) =>
            {
                _settings.Token = Guid.NewGuid().ToString("N");

                var ip = GetLanIPv4() ?? "LAN-IP";
                var url = $"http://{ip}:{_settings.Port}/push-photo?token={_settings.Token}";

                Title = $"PhotoPopupReceiver - Listening on :{_settings.Port}";
                UrlText.Text = $"Endpoint:\n{url}";
                await _receiver.StartAsync(_settings, OnPhotoSavedAsync);
            };
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
