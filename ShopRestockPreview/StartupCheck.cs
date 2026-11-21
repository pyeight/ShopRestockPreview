using System.Runtime.InteropServices;
// ReSharper disable InconsistentNaming

namespace ShopRestockPreview;

internal static class StartupCheck
{
    private const string RequiredGameVersion = BuildInfo.ExactGameVersion;
    private const string VersionUrl = "https://raw.githubusercontent.com/pyeight/ShopRestockPreview/refs/heads/master/latestModVersion.txt";

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

    private const uint MB_OK = 0x00000000;
    private const uint MB_ICON_ERROR = 0x00000010;
    private const uint MB_ICON_WARNING = 0x00000030;

    private static bool checkedOnce;

    public static void Initialize()
    {
        if (checkedOnce) return;

        checkedOnce = true;

        var installedGameVersion = MelonLoader.InternalUtils.UnityInformationHandler.GameVersion;

        var versionParts = installedGameVersion.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (versionParts.Length > 0)
        {
            installedGameVersion = versionParts[0];
        }

        switch (CompareVersions(installedGameVersion, RequiredGameVersion))
        {
            case < 0:
                ShowMessageBox(
                    $"{BuildInfo.Name} is incompatible with this game version.\n\n" +
                    $"Required: {RequiredGameVersion}\n" +
                    $"Detected: {installedGameVersion}",
                    $"{BuildInfo.Name} – Incompatible Game Version",
                    MB_OK | MB_ICON_ERROR, true
                );
                Application.Quit();
                return;
            case > 0:
                ShowMessageBox(
                    $"You are running a newer game version than {BuildInfo.Name} was built for.\n\n" +
                    $"Required: {RequiredGameVersion}\n" +
                    $"Detected: {installedGameVersion}\n\n" +
                    "The mod may still work, but issues are possible.",
                    $"{BuildInfo.Name} – Newer Game Version Detected",
                    MB_OK | MB_ICON_WARNING, false
                );
                break;
        }

        Task.Run(async () => await CheckModVersionAsync());
    }

    private static async Task CheckModVersionAsync()
    {
        try
        {
            // ReSharper disable once ShortLivedHttpClient
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);

            const string currentModVersion = BuildInfo.Version;
            var latestVersion = (await client.GetStringAsync(VersionUrl)).Trim();

            switch (CompareVersions(currentModVersion, latestVersion))
            {
                case < 0:
                    ShowMessageBox(
                        $"Your {BuildInfo.Name} mod is outdated!\n\n" +
                        $"Your version: {currentModVersion}\n" +
                        $"Latest version: {latestVersion}\n\n" +
                        "Get the new version from NexusMods.",
                        $"{BuildInfo.Name} – Update Available",
                        MB_OK | MB_ICON_WARNING, false
                    );
                    break;

                case > 0:
                    ShowMessageBox(
                        $"Your {BuildInfo.Name} mod version is newer than the latest release.\n\n" +
                        $"Your version: {currentModVersion}\n" +
                        $"Latest version: {latestVersion}\n\n" +
                        "This version is not officially supported and may not work correctly.",
                        $"{BuildInfo.Name} – Unsupported Version",
                        MB_OK | MB_ICON_WARNING, false
                    );
                    break;
            }
        }
        catch { /* ignore */ }
    }

    private static void ShowMessageBox(string text, string caption, uint type, bool error)
    {
        try
        {
            MessageBoxW(IntPtr.Zero, text, caption, type);

            if (error)
                Main.Log.Error($"{caption}\n{text}");
            else
                Main.Log.Warning($"{caption}\n{text}");
        }
        catch (Exception ex)
        {
            Main.Log.Error($"{caption}\n{text}\n{ex}");
        }
    }

    private static int CompareVersions(string version1, string version2)
    {
        var v1Parts = version1.Split('.');
        var v2Parts = version2.Split('.');
        var maxLength = Math.Max(v1Parts.Length, v2Parts.Length);

        for (var i = 0; i < maxLength; i++)
        {
            var v1 = i < v1Parts.Length && int.TryParse(v1Parts[i], out var v1Val) ? v1Val : 0;
            var v2 = i < v2Parts.Length && int.TryParse(v2Parts[i], out var v2Val) ? v2Val : 0;

            if (v1 < v2)
                return -1;

            if (v1 > v2)
                return 1;
        }

        return 0;
    }
}