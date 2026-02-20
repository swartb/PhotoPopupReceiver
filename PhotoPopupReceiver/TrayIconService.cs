using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Forms;

namespace PhotoPopupReceiver
{
    public class TrayIconService : IDisposable
    {
        public static TrayIconService? Current { get; private set; }

        private const string IconResourceFileName = "6d8fd7e3-a1df-4e03-96d2-f2c919887df7.ico";

        private readonly NotifyIcon _notifyIcon;
        private readonly Icon _icon;

        public TrayIconService(Action onOpen, Action onQuit)
        {
            Current = this;
            var appIcon = TryGetAppIcon() ?? System.Drawing.SystemIcons.Application;
            _icon = new Icon(appIcon, SystemInformation.SmallIconSize);
            if (!ReferenceEquals(appIcon, System.Drawing.SystemIcons.Application))
                appIcon.Dispose();

            _notifyIcon = new NotifyIcon
            {
                Text = "PhotoPopupReceiver",
                Icon = _icon,
                Visible = true
            };

            _notifyIcon.DoubleClick += (_, __) => onOpen();

            var menu = new ContextMenuStrip();
            menu.Items.Add("Open", null, (_, __) => onOpen());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Quit", null, (_, __) => onQuit());

            _notifyIcon.ContextMenuStrip = menu;
        }

        public void ShowBalloon(string title, string message)
        {
            _notifyIcon.BalloonTipTitle = title;
            _notifyIcon.BalloonTipText = message;
            _notifyIcon.ShowBalloonTip(1500);
        }

        public void Dispose()
        {
            if (ReferenceEquals(Current, this))
                Current = null;

            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _icon.Dispose();
        }

        private static Icon? TryGetAppIcon()
        {
            var resourceIcon = TryGetPackResourceIcon();
            if (resourceIcon is not null)
                return resourceIcon;

            try
            {
                var exePath = Assembly.GetEntryAssembly()?.Location;
                if (string.IsNullOrWhiteSpace(exePath))
                    return null;

                return Icon.ExtractAssociatedIcon(exePath);
            }
            catch
            {
                return null;
            }
        }

        private static Icon? TryGetPackResourceIcon()
        {
            try
            {
                var info = System.Windows.Application.GetResourceStream(
                    new Uri($"pack://application:,,,/{IconResourceFileName}", UriKind.Absolute));
                if (info?.Stream is null)
                    return null;

                using var stream = info.Stream;
                using var icon = new Icon(stream, SystemInformation.SmallIconSize);
                return (Icon)icon.Clone();
            }
            catch
            {
                return null;
            }
        }
    }
}
