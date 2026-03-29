using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Windows;
using AresAssistant.Core;
using AresAssistant.Helpers;

namespace AresAssistant.Views;

public partial class UpdateWindow : Window
{
    private readonly ReleaseInfo _release;
    private CancellationTokenSource? _cts;

    public UpdateWindow(ReleaseInfo release)
    {
        InitializeComponent();
        _release = release;
        MouseLeftButtonDown += (_, _) => DragMove();

        var current = typeof(App).Assembly.GetName().Version?.ToString(3) ?? "?";
        TxtVersion.Text = $"v{current}  →  {release.TagName}";
        TxtNotes.Text = string.IsNullOrWhiteSpace(release.Notes) ? "(sin notas de versión)" : release.Notes;
    }

    private void BtnLater_Click(object sender, RoutedEventArgs e) => Close();

    private void BtnUpdate_Click(object sender, RoutedEventArgs e)
    {
        PanelInfo.Visibility = Visibility.Collapsed;
        PanelProgress.Visibility = Visibility.Visible;
        _cts = new CancellationTokenSource();
        _ = DownloadAndApplyAsync(_cts.Token);
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        if (_cts != null && !_cts.IsCancellationRequested)
            _cts.Cancel();
        else
            Close();
    }

    private async Task DownloadAndApplyAsync(CancellationToken ct)
    {
        var appDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule!.FileName)!;
        var tempDir = Path.Combine(Path.GetTempPath(), "ares_update");
        var zipPath = Path.Combine(Path.GetTempPath(), "ares_update.zip");

        try
        {
            // ── Fase 1: Descargar zip ──
            TxtStep.Text = "Descargando actualización...";
            TxtStatus.Text = "Conectando con GitHub...";

            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) })
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd("ARES/1.0");
                using var resp = await http.GetAsync(_release.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
                resp.EnsureSuccessStatusCode();
                var total = resp.Content.Headers.ContentLength ?? 0;

                using var stream = await resp.Content.ReadAsStreamAsync(ct);
                using var fs = File.Create(zipPath);
                var buffer = new byte[81920];
                long downloaded = 0;
                int read;

                while ((read = await stream.ReadAsync(buffer, ct)) > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    await fs.WriteAsync(buffer.AsMemory(0, read), ct);
                    downloaded += read;

                    if (total > 0)
                    {
                        var pct = (double)downloaded / total;
                        SetProgress(pct * 0.7);
                        TxtPercentage.Text = $"{pct * 70:F0} %";
                        TxtSize.Text = $"{FormatHelper.FormatBytes(downloaded)} / {FormatHelper.FormatBytes(total)}";
                    }
                    TxtStatus.Text = "Descargando...";
                }
            }

            // ── Fase 2: Extraer ──
            TxtStep.Text = "Extrayendo archivos...";
            TxtStatus.Text = "";
            SetProgress(0.72);
            TxtPercentage.Text = "72 %";

            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);

            await Task.Run(() => ZipFile.ExtractToDirectory(zipPath, tempDir), ct);
            try { File.Delete(zipPath); } catch { }

            SetProgress(0.90);
            TxtPercentage.Text = "90 %";

            // ── Fase 3: Escribir script de reemplazo ──
            TxtStep.Text = "Preparando instalación...";
            TxtStatus.Text = "ARES se reiniciará automáticamente";
            SetProgress(0.95);
            TxtPercentage.Text = "95 %";

            var pid = Process.GetCurrentProcess().Id;
            var batPath = Path.Combine(Path.GetTempPath(), "ares_updater.bat");
            var exePath = Path.Combine(appDir, "AresAssistant.exe");

            // El bat espera a que el proceso actual cierre, copia los archivos y relanza
            var bat = $"""
                @echo off
                :wait
                tasklist /fi "PID eq {pid}" 2>nul | find "{pid}" >nul
                if not errorlevel 1 (
                    timeout /t 1 /nobreak >nul
                    goto wait
                )
                xcopy /s /y "{tempDir}\*" "{appDir}\"
                rd /s /q "{tempDir}"
                start "" "{exePath}"
                del "%~f0"
                """;

            await File.WriteAllTextAsync(batPath, bat, ct);

            SetProgress(1.0);
            TxtPercentage.Text = "100 %";
            TxtStep.Text = "✓ Listo — reiniciando...";

            // Lanzar el bat y cerrar la app
            Process.Start(new ProcessStartInfo
            {
                FileName = batPath,
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                UseShellExecute = true
            });

            App.IsExiting = true;
            Application.Current.Dispatcher.Invoke(Application.Current.Shutdown);
        }
        catch (OperationCanceledException)
        {
            TxtStep.Text = "Actualización cancelada";
            TxtStatus.Text = "";
            BtnCancel.Content = "Cerrar";
            try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch { }
        }
        catch (Exception ex)
        {
            TxtStep.Text = "Error durante la actualización";
            TxtStatus.Text = ex.Message;
            TxtPercentage.Text = "Error";
            BtnCancel.Content = "Cerrar";
        }
    }

    private void SetProgress(double pct)
    {
        pct = Math.Clamp(pct, 0, 1);
        Dispatcher.Invoke(() =>
        {
            var w = ProgressBarGrid.ActualWidth;
            if (w > 0) ProgressFill.Width = w * pct;
        });
    }
}
