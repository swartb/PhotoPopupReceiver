using System;
using System.IO;

namespace PhotoPopupReceiver
{
    public class AppSettings
    {
        public int Port { get; set; } = 5055;
        public string Token { get; set; } = "changeme";
        public bool AutoPopup { get; set; } = true;
        public bool AutoCopyToClipboard { get; set; } = false;

        public string SaveFolder { get; set; } =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "PhotoPopups");
    }
}
