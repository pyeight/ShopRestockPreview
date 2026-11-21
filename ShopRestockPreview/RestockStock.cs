using Il2CppInterop.Runtime.InteropTypes;
using Il2CppMonomiPark.SlimeRancher.Event;
using Il2CppMonomiPark.SlimeRancher.Shop;
using Il2CppMonomiPark.SlimeRancher.Shop.Runtime;
using Il2CppMonomiPark.SlimeRancher.UI.Popup;

namespace ShopRestockPreview;

internal static class RestockStock
{
    private sealed class Entry
    {
        internal readonly PreviewPopupStandaloneConfig Config;
        internal List<ShopRuntimeItem> Items = new();

        internal Entry(PreviewPopupStandaloneConfig config) => Config = config;
    }

    private static readonly Dictionary<IntPtr, Entry> Entries = new();
    
    internal static void Capture(ShopRuntime runtime, IShopDefinition shopDef)
    {
        var config = shopDef.ShopRestockMessage;

        if (!config) return;

        if (!Entries.TryGetValue(config.Pointer, out var entry))
        {
            entry = new Entry(config);
            Entries[config.Pointer] = entry;
        }

        var items = CollectRestockedItems(runtime);

        foreach (var item in entry.Items)
            item.ReleaseResources();

        foreach (var item in items)
            item.AcquireResources();

        entry.Items = items;
    }

    internal static bool? WillShowPopup(ShopRuntime runtime, IShopDefinition shopDef)
    {
        var condition = shopDef.ShowShopRestockCondition;

        if (condition == null || !runtime._eventDirector) return null;

        return condition.IsSatisfied(runtime._eventDirector.Cast<IEventDirector>());
    }

    internal static List<ShopRuntimeItem>? Find(PreviewPopupViewModel? data)
    {
        if (data == null) return null;

        foreach (var entry in Entries.Values)
        {
            if (Matches(entry.Config, data)) return entry.Items;
        }

        return null;
    }

    internal static void Clear()
    {
        foreach (var entry in Entries.Values)
        {
            foreach (var item in entry.Items)
            {
                try
                {
                    item.ReleaseResources();
                }
                catch
                {
                    /* ignore */
                }
            }
        }

        Entries.Clear();
    }

    private static List<ShopRuntimeItem> CollectRestockedItems(ShopRuntime runtime)
    {
        var items = new List<ShopRuntimeItem>();

        if (!runtime.IsDoneLoading || !runtime._eventDirector)
            return items;

        var eventDirector = runtime._eventDirector.Cast<IEventDirector>();
        var categories    = new List<ShopCategoryRuntime>();

        foreach (var category in Iterate(runtime.GetActiveCategories(eventDirector)))
        {
            if (category.HasAnyRestock && category.IsDoneLoading)
                categories.Add(category);
        }

        categories.Sort((a, b) => a.DisplaySortOrder.CompareTo(b.DisplaySortOrder));

        foreach (var category in categories)
        {
            foreach (var item in Snapshot(category.GetRuntimeItems(eventDirector)))
            {
                if (!item.IsSoldOut)
                    items.Add(item);
            }
        }

        return items;
    }

    private static List<T> Snapshot<T>(Il2CppSystem.Collections.Generic.IEnumerable<T>? source) where T : Il2CppObjectBase
    {
        var copy = new List<T>();

        if (source == null)
            return copy;

        var list = source.TryCast<Il2CppSystem.Collections.Generic.List<T>>();

        if (list == null)
        {
            copy.AddRange(Iterate(source));
            return copy;
        }

        for (var i = 0; i < list.Count; i++)
            copy.Add(list[i]);

        return copy;
    }

    private static bool Matches(PreviewPopupStandaloneConfig config, PreviewPopupViewModel data)
    {
        var anySet = false;

        return Same(config._title,  data.Title,  ref anySet)
            && Same(config._header, data.Header, ref anySet)
            && Same(config._intro,  data.Intro,  ref anySet)
            && Same(config._hint,   data.Hint,   ref anySet)
            && Same(config._icon,   data.Icon,   ref anySet)
            && anySet;
    }

    private static bool Same(Il2CppObjectBase? left, Il2CppObjectBase? right, ref bool anySet)
    {
        var leftPointer  = left?.Pointer  ?? IntPtr.Zero;
        var rightPointer = right?.Pointer ?? IntPtr.Zero;

        anySet |= leftPointer != IntPtr.Zero;

        return leftPointer == rightPointer;
    }

    private static IEnumerable<T> Iterate<T>(Il2CppSystem.Collections.Generic.IEnumerable<T>? source)
    {
        if (source == null)
            yield break;

        var enumerator = source.GetEnumerator();
        var steps      = enumerator.Cast<Il2CppSystem.Collections.IEnumerator>();

        while (steps.MoveNext())
            yield return enumerator.Current;
    }
}