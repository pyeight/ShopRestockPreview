using MelonLoader;
using MelonLoader.Logging;
using Starlight.Expansion;
using Starlight.Enums;
using Starlight.Storage;

namespace ShopRestockPreview;

[StarlightLoadExpansion]
public sealed class Main : StarlightExpansionV01
{
    internal static readonly MelonLogger.Instance Log = new(BuildInfo.Name, ColorARGB.FromArgb(35, 186, 206));

    protected override StarlightPackageInfo info => new()
    {
        ID           = BuildInfo.ID,
        Name         = BuildInfo.Name,
        Author       = BuildInfo.Author,
        CoAuthors    = BuildInfo.CoAuthors,
        Contributors = BuildInfo.Contributors,
        Description  = BuildInfo.Description,
        SourceCode   = BuildInfo.SourceCode,
        Version      = BuildInfo.Version,
        Nexus        = BuildInfo.Nexus,
        Discord      = BuildInfo.Discord,
        UsePrism     = BuildInfo.UsePrism,

        LoadTime               = ExpansionLoadTime.Startup,
        UnloadTime             = ExpansionUnloadTime.Never,
        MultiplayerRequirement = MultiplayerRequirement.Client
    };

    private static MelonPreferences_Category preferences = null!;

    internal static int MaxShownItems => preferences.GetEntry<int>("max_shown_items").Value;

    internal static bool AlwaysShowRestock => preferences.GetEntry<bool>("always_show_restock").Value;

    public override void OnLateInitialize()
    {
        preferences = MelonPreferences.CreateCategory("ShopRestockPreview");

        preferences.CreateEntry("max_shown_items", 10, display_name: "Max Shown Items");
        preferences.CreateEntry("always_show_restock", true, display_name: "Always Show Restock",
            description: "Show the restock popup on every restock, even when the shop wasn't opened since the last restock.");
    }

    public override void OnUpdate() => RestockPopupView.Tick();

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        if (sceneName == "SystemCore")
            StartupCheck.Initialize();

        if (sceneName.Contains("MainMenu"))
            RestockStock.Clear();
    }
}