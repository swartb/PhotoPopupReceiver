using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace PhotoPopupReceiver
{
    public class PhotoReceiver
    {
        private IHost? _host;

        public async Task StartAsync(AppSettings settings, Func<string, Task> onSaved)
        {
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
        }

        public Task StopAsync() => _host?.StopAsync() ?? Task.CompletedTask;
    }
}
