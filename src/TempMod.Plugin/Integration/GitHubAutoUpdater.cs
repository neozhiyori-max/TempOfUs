using BepInEx.Logging;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;

namespace TempMod.Plugin.Integration;

/// <summary>
/// GitHub Releasesの最新公開版を起動時に確認する。更新本体はゲーム終了後にPowerShellが展開するため、
/// 読み込み中のDLLを上書きせず、全PCが同じ公開版へ揃えられる。
/// </summary>
internal static class GitHubAutoUpdater
{
    private const string Owner = "neozhiyori-max";
    private const string Repository = "TempOfUs";
    private const string AssetName = "tempMOD-public.zip";
    private const string ApiUrl = "https://api.github.com/repos/" + Owner + "/" + Repository + "/releases/latest";
    private static int _started;

    internal static void CheckInBackground(ManualLogSource log)
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
            return;
        _ = Task.Run(async () =>
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("tempMOD-updater/0.1");
                using var response = await client.GetAsync(ApiUrl).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    log.LogInfo($"tempMOD更新確認: 公開リリースはまだありません ({(int)response.StatusCode})。");
                    return;
                }

                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                var root = document.RootElement;
                var tag = root.GetProperty("tag_name").GetString() ?? string.Empty;
                if (!IsNewer(tag, TempModPlugin.PluginVersion))
                    return;

                string? downloadUrl = null;
                foreach (var asset in root.GetProperty("assets").EnumerateArray())
                {
                    if (string.Equals(asset.GetProperty("name").GetString(), AssetName, StringComparison.Ordinal))
                    {
                        downloadUrl = asset.GetProperty("browser_download_url").GetString();
                        break;
                    }
                }
                if (string.IsNullOrWhiteSpace(downloadUrl) || !Uri.TryCreate(downloadUrl, UriKind.Absolute, out var uri) || uri.Host is not "github.com" and not "objects.githubusercontent.com")
                {
                    log.LogWarning("tempMOD更新確認: 正式な公開アセットが見つかりませんでした。");
                    return;
                }

                var pluginDirectory = Path.GetDirectoryName(typeof(TempModPlugin).Assembly.Location);
                if (string.IsNullOrWhiteSpace(pluginDirectory))
                    return;
                var gameRoot = Directory.GetParent(pluginDirectory)?.Parent?.Parent?.FullName;
                if (string.IsNullOrWhiteSpace(gameRoot))
                    return;

                var stagingDirectory = Path.Combine(pluginDirectory, ".tempMOD-update");
                Directory.CreateDirectory(stagingDirectory);
                var zipPath = Path.Combine(stagingDirectory, AssetName);
                await using (var remote = await client.GetStreamAsync(uri).ConfigureAwait(false))
                await using (var local = File.Create(zipPath))
                    await remote.CopyToAsync(local).ConfigureAwait(false);

                QueueApplyAfterExit(zipPath, gameRoot);
                log.LogInfo($"tempMOD {tag} をダウンロードしました。ゲーム終了後に自動で適用され、次回起動時から有効になります。");
            }
            catch (Exception exception)
            {
                // ネットワーク不通やGitHub側の一時エラーではゲーム本体を妨げない。
                log.LogWarning($"tempMOD更新確認をスキップしました: {exception.Message}");
            }
        });
    }

    private static void QueueApplyAfterExit(string zipPath, string gameRoot)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var processId = Environment.ProcessId;
        var scriptPath = Path.Combine(Path.GetDirectoryName(zipPath)!, "apply-tempMOD-update.ps1");
        var script = "$ErrorActionPreference = 'Stop'" + Environment.NewLine
            + $"Wait-Process -Id {processId}" + Environment.NewLine
            + "Start-Sleep -Seconds 2" + Environment.NewLine
            + $"Expand-Archive -LiteralPath '{EscapePowerShell(zipPath)}' -DestinationPath '{EscapePowerShell(gameRoot)}' -Force" + Environment.NewLine
            + $"Remove-Item -LiteralPath '{EscapePowerShell(zipPath)}' -Force -ErrorAction SilentlyContinue" + Environment.NewLine
            + "Remove-Item -LiteralPath $PSCommandPath -Force -ErrorAction SilentlyContinue" + Environment.NewLine;
        File.WriteAllText(scriptPath, script);
        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
            CreateNoWindow = true,
            UseShellExecute = false,
        });
    }

    private static bool IsNewer(string tag, string current)
    {
        var normalizedTag = tag.TrimStart('v', 'V');
        return Version.TryParse(normalizedTag, out var remote) && Version.TryParse(current, out var local) && remote > local;
    }

    private static string EscapePowerShell(string value) => value.Replace("'", "''");
}
