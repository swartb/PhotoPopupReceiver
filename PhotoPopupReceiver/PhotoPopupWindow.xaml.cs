using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace PhotoPopupReceiver
{
    public partial class PhotoPopupWindow : Window
    {
        private string? _currentPath;

        public PhotoPopupWindow()
        {
            InitializeComponent();
            Loaded += (_, __) => PositionBottomRight();
        }

        public void SetImage(string path)
        {
            _currentPath = path;

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(Path.GetFullPath(path));
            bmp.EndInit();
            bmp.Freeze();

            PreviewImage.Source = bmp;
        }

        public void PositionBottomRight()
        {
            var wa = SystemParameters.WorkArea;
            const double margin = 16;
            Left = wa.Right - ActualWidth - margin;
            Top = wa.Bottom - ActualHeight - margin;
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPath is null) return;
            ClipboardHelper.CopyImageFileToClipboard(_currentPath);
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
