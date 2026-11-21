using HarmonyLib;
using Il2CppMonomiPark.SlimeRancher.Player;
using Il2CppMonomiPark.SlimeRancher.Shop;
using Il2CppMonomiPark.SlimeRancher.Shop.Runtime;
using Il2CppMonomiPark.SlimeRancher.UI.Popup;

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
    public static void Prefix(ShopRuntime runtime, bool hasNewStock, IShopDefinition shopDef)
    {
        if (!hasNewStock || runtime == null || shopDef == null)
            return;

        try
        {
            RestockStock.Capture(runtime, shopDef);
        }
        catch (Exception ex)
        {
            Main.Log.Error($"Couldn't read the restocked items: {ex}");
        }
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