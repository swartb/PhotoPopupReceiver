using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PhotoPopupReceiver
{
    public class PhotoReceiver
    {
        private IHost? _host;

        public async Task StartAsync(AppSettings settings, Func<string, Task> onSaved, Func<string, Task>? onTextReceived = null)
        {
            File.AppendAllText("receiver.log", $"StartAsync called. Port={settings.Port}\n");
            System.Diagnostics.Debug.WriteLine($"StartAsync called. Port={settings.Port}");
            if (_host != null) return; // al gestart

            _host = Host.CreateDefaultBuilder()
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddDebug();       // <-- zichtbaar in Output window
                })
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

                                    // AUTH via header
                                    string auth = "";
                                    if (req.Headers.TryGetValue("X-Auth", out var hv))
                                    {
                                        auth = hv.ToString().Trim();
                                    }

                                    var expected = (settings.Password ?? "").Trim();

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
                                            await context.Response.WriteAsync("Unauthorized.");
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

                                endpoints.MapPost("/push-text", async context =>
                                {
                                    var req = context.Request;

                                    // AUTH via header (same as /push-photo)
                                    string auth = "";
                                    if (req.Headers.TryGetValue("X-Auth", out var hv))
                                    {
                                        auth = hv.ToString().Trim();
                                    }

                                    var expected = (settings.Password ?? "").Trim();

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
                                            await context.Response.WriteAsync("Unauthorized.");
                                            return;
                                        }
                                    }

                                    string? text = null;

                                    // Accept form data (application/x-www-form-urlencoded or multipart/form-data)
                                    // with a "text" field, or fall back to reading the raw body as plain text.
                                    if (req.HasFormContentType)
                                    {
                                        var form = await req.ReadFormAsync();
                                        text = form["text"].FirstOrDefault();
                                    }
                                    else
                                    {
                                        using var reader = new System.IO.StreamReader(req.Body, System.Text.Encoding.UTF8);
                                        text = await reader.ReadToEndAsync();
                                    }

                                    if (string.IsNullOrWhiteSpace(text))
                                    {
                                        context.Response.StatusCode = 400;
                                        await context.Response.WriteAsync("text missing or empty");
                                        return;
                                    }

                                    if (onTextReceived != null)
                                        await onTextReceived(text);

                                    context.Response.StatusCode = 200;
                                    await context.Response.WriteAsync("ok");
                                });
                            });
                        });
                })
                .Build();

            await _host.StartAsync();
            File.AppendAllText("receiver.log", $"Host started OK. Url=http://0.0.0.0:{settings.Port}\n");
            System.Diagnostics.Debug.WriteLine($"Host started OK. Url=http://0.0.0.0:{settings.Port}");
        }

        public async Task StopAsync()
        {
            if (_host == null) return;
            await _host.StopAsync();
            _host.Dispose();
            _host = null;
        }
    }
}
