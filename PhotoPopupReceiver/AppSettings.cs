using System;
using System.IO;
using System.Text.Json;

namespace PhotoPopupReceiver
{
    /// <summary>
    /// Holds all user-configurable settings for the application.
    /// An instance of this class is created at startup and passed to the HTTP listener
    /// (<see cref="PhotoReceiver"/>) as well as consulted when a photo arrives.
    /// </summary>
    public class AppSettings
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        public static string SettingsDirectory { get; } =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PhotoPopupReceiver");

        public static string SettingsPath { get; } =
            Path.Combine(SettingsDirectory, "settings.json");

        /// <summary>
        /// The TCP port on which the HTTP server listens for incoming photo upload requests.
        /// Must be a free port on the host machine. Default is <c>5055</c>.
        /// </summary>
        public int Port { get; set; } = 5055;

        /// <summary>
        /// When <see langword="true"/>, a floating popup notification window is automatically
        /// displayed in the bottom-right corner of the screen whenever a new photo is received.
        /// Set to <see langword="false"/> to receive photos silently without any visible popup.
        /// Default is <see langword="true"/>.
        /// </summary>
        public bool AutoPopup { get; set; } = true;

        /// <summary>
        /// When <see langword="true"/>, each received photo is automatically placed onto the
        /// Windows clipboard immediately after it is saved, so the user can paste it directly
        /// into another application without any extra steps.
        /// Default is <see langword="false"/>.
        /// </summary>
        public bool AutoCopyToClipboard { get; set; } = false;
        public bool RequirePassword { get; set; } = false;
        public string Password { get; set; } = "";

        /// <summary>
        /// The local directory where received photo files are saved.
        /// Defaults to a <c>PhotoPopups</c> sub-folder inside the current user's
        /// <em>My Pictures</em> folder (e.g. <c>C:\Users\&lt;name&gt;\Pictures\PhotoPopups</c>).
        /// The folder is created automatically if it does not yet exist.
        /// </summary>
        public string SaveFolder { get; set; } =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "PhotoPopups");

        public static AppSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                    return new AppSettings();

                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
            catch
            {
                return new AppSettings();
            }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(SettingsDirectory);
                var json = JsonSerializer.Serialize(this, JsonOptions);
                File.WriteAllText(SettingsPath, json);
            }
            catch
            {
                // ignore persistence failures
            }
        }
    }
}
