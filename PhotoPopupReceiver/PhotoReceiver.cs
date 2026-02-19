// PhotoReceiver.cs

// This class handles the reception of photo data and related processing.
using System;
<<<<<<< HEAD
using System.Diagnostics;
using System.IO;
=======
>>>>>>> 6f5470a66bde67bd6f654e554b2581334c64c41d
using System.Threading.Tasks;

namespace PhotoPopupReceiver
{
    /// <summary>
    /// Responsible for receiving incoming photo data from a remote sender and
    /// orchestrating any follow-up processing (e.g. saving, format conversion).
    /// </summary>
    /// <remarks>
    /// In the full implementation this class starts a lightweight HTTP endpoint
    /// (via ASP.NET Core Kestrel) that accepts multipart POST requests containing
    /// image payloads.  Each received image is validated against the shared secret
    /// token defined in <see cref="AppSettings.Token"/>, saved to the directory
    /// specified by <see cref="AppSettings.SaveFolder"/>, and then surfaced to the
    /// UI layer through a callback delegate so that a popup can be shown and/or the
    /// image can be copied to the clipboard.
    /// </remarks>
    public class PhotoReceiver
    {
        // Stores the raw bytes of the most recently received photo.
        private byte[]? photoData;

        /// <summary>
        /// Initializes a new instance of <see cref="PhotoReceiver"/>.
        /// Any one-time setup required before the listener is started (e.g. creating
        /// the save directory) should be performed here.
        /// </summary>
        public PhotoReceiver()
        {
<<<<<<< HEAD
            _host = Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseKestrel()
                        .UseUrls($"http://0.0.0.0:{settings.Port}")
.Configure(app =>
{
    app.UseRouting();

    app.UseEndpoints(endpoints =>
    {
        endpoints.MapPost("/push-photo", async context =>
        {
            var req = context.Request;
            // AUTH
            string auth = "";
            if (context.Request.Headers.TryGetValue("X-Auth", out var hv) ||
                context.Request.Headers.TryGetValue("X-Auth-Token", out hv))
            {
                auth = hv.ToString().Trim();
            }
            var expected = (settings.Password ?? "").Trim();


            Debug.WriteLine($"AUTH require={settings.RequirePassword} got='{auth}' expected='{expected}'");
            Debug.WriteLine($"EXPECTED: '{expected}'");
            Debug.WriteLine($"AUTH: '{auth}'");


            if (settings.RequirePassword)
            {
                if (string.IsNullOrWhiteSpace(expected))
                {
                    context.Response.StatusCode = 500;
                    await context.Response.WriteAsync("Server misconfigured: password required but empty.");
                    return;
                }

                if (!string.Equals(auth, expected, StringComparison.Ordinal))
                {
                    context.Response.StatusCode = 401;
                    context.Response.ContentType = "text/plain; charset=utf-8";
                    await context.Response.WriteAsync($"Unauthorized. Got='{auth}' Expected='{expected}'");
                    return;
                }
            }

            if (!req.HasFormContentType)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("multipart/form-data expected");
                return;
            }

            var form = await req.ReadFormAsync();
            var file = form.Files.GetFile("file");
            if (file == null || file.Length == 0)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("file missing");
                return;
            }

            var dayFolder = Path.Combine(settings.SaveFolder, DateTime.Now.ToString("yyyy-MM-dd"));
            Directory.CreateDirectory(dayFolder);

            var ext = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(ext)) ext = ".jpg";

            var fileName = DateTime.Now.ToString("HH-mm-ss_fff") + ext;
            var savePath = Path.Combine(dayFolder, fileName);

            await using (var fs = File.Create(savePath))
                await file.CopyToAsync(fs);

            await onSaved(savePath);

            context.Response.StatusCode = 200;
            await context.Response.WriteAsync("ok");
        });
    });
});
                })
                .Build();

            await _host.StartAsync();
=======
            // Initialization code can go here if needed.
>>>>>>> 6f5470a66bde67bd6f654e554b2581334c64c41d
        }

        /// <summary>
        /// Starts the HTTP listener and invokes <paramref name="onPhotoSaved"/> for each
        /// received photo.  This stub exists so the project compiles while the full
        /// Kestrel-based implementation is pending.
        /// </summary>
        public Task StartAsync(AppSettings settings, Func<string, Task> onPhotoSaved)
        {
            // Full implementation will start an ASP.NET Core Kestrel listener here.
            return Task.CompletedTask;
        }

        /// <summary>
        /// Accepts raw image bytes from an incoming request and triggers processing.
        /// </summary>
        /// <param name="data">
        /// The raw byte payload of the received image (e.g. JPEG or PNG file data).
        /// </param>
        public void ReceivePhoto(byte[] data)
        {
            // Store the received bytes so they are available to ProcessPhoto.
            photoData = data;
            // Delegate the actual work (saving, converting, notifying) to ProcessPhoto.
            ProcessPhoto();
        }

        /// <summary>
        /// Processes the raw photo data stored in <see cref="photoData"/>.
        /// Typical responsibilities include decoding the image, writing it to disk
        /// inside <see cref="AppSettings.SaveFolder"/>, and invoking any registered
        /// UI callbacks (e.g. showing a popup or copying to the clipboard).
        /// </summary>
        private void ProcessPhoto()
        {
            // Convert the photo data into a usable format or perform transformations.
            // This is where the main logic for handling photos would be implemented.
        }
    }
}