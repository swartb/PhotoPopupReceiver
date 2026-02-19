using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace PhotoPopupReceiver
{
    public static class ClipboardHelper
    {
        public static void CopyImageFileToClipboard(string filePath)
        {
            if (!File.Exists(filePath))
                return;

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad; // voorkomt file lock
            bmp.UriSource = new Uri(Path.GetFullPath(filePath));
            bmp.EndInit();
            bmp.Freeze(); // thread safe

            Clipboard.SetImage(bmp);
        }
    }
}
