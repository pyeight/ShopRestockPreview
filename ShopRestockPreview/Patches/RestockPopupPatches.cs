using HarmonyLib;
using Il2CppMonomiPark.SlimeRancher.Player;
using Il2CppMonomiPark.SlimeRancher.Shop;
using Il2CppMonomiPark.SlimeRancher.Shop.Runtime;
using Il2CppMonomiPark.SlimeRancher.UI.Popup;
using Il2CppMonomiPark.SlimeRancher.Util;

namespace ShopRestockPreview.Patches;

[HarmonyPatch(typeof(ShopRuntime), nameof(ShopRuntime.HandleShopRestock))]
internal static class HandleShopRestockPatch
{
    public static void Prefix(ShopRuntime __instance)
    {
        if (!Main.AlwaysShowRestock || __instance == null) return;

        try
        {
            __instance._hasNewStock = false;
        }
        catch (Exception ex)
        {
            Main.Log.Error($"Couldn't reset the shop's new stock flag: {ex}");
        }
    }
}

[HarmonyPatch(typeof(PlayerReminders), nameof(PlayerReminders.HandleShopsRestocked))]
internal static class HandleShopsRestockedPatch
{
    public static bool Prefix(ShopRuntime runtime, bool hasNewStock, IShopDefinition shopDef)
    {
        if (!hasNewStock || runtime == null || shopDef == null)
            return true;

        try
        {
            RestockStock.Capture(runtime, shopDef);
        }
        catch (Exception ex)
        {
            Main.Log.Error($"Couldn't read the restocked items: {ex}");
        }

        try
        {
            return !RefreshQueuedPopup(shopDef);
        }
        catch (Exception ex)
        {
            Main.Log.Error($"Couldn't check for a queued restock popup: {ex}");
            return true;
        }
    }

    private static bool RefreshQueuedPopup(IShopDefinition shopDef)
    {
        var config = shopDef.ShopRestockMessage;

        if (!config || !SceneContextUtility.TryGetPopupDirector(out var director) || !director)
            return false;

        foreach (var handler in director._popupHandlers)
        {
            var stack = handler.TryCast<PreviewPopupStack>();

            if (!stack || !IsQueued(stack!, config)) continue;

            stack!.UpdatePopups();
            return true;
        }

        return false;
    }

    private static bool IsQueued(PreviewPopupStack stack, PreviewPopupStandaloneConfig config)
    {
        var queued = stack._popupData;

        for (var i = 0; i < queued.Count; i++)
        {
            if (RestockStock.Matches(config, queued[i])) return true;
        }

        return false;
    }
}

[HarmonyPatch(typeof(PreviewPopup), nameof(PreviewPopup.SetData))]
internal static class PreviewPopupSetDataPatch
{
    public static void Postfix(PreviewPopup __instance, PreviewPopupViewModel data)
    {
        try
        {
            RestockPopupView.Show(__instance, RestockStock.Find(data));
        }
        catch (Exception ex)
        {
            Main.Log.Error($"Couldn't show the restocked items: {ex}");
        }
    }
}