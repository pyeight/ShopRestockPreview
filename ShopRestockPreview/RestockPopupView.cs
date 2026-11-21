using Il2CppMonomiPark.SlimeRancher.Economy;
using Il2CppMonomiPark.SlimeRancher.Shop.Runtime;
using Il2CppMonomiPark.SlimeRancher.UI.Framework.Data;
using Il2CppMonomiPark.SlimeRancher.UI.Framework.Layout;
using Il2CppMonomiPark.SlimeRancher.UI.Popup;
using Il2CppMonomiPark.SlimeRancher.UI.SDFUI;
using Il2CppTMPro;
using UnityEngine.UI;

namespace ShopRestockPreview;

internal static class RestockPopupView
{
    private const string ContainerName = "ShopRestockPreview";
    private const string BundlePrefix  = "ShopRestockPreview Bundle ";
    private const string TilePrefix    = "Item";
    private const string LabelName     = "Label";

    private const float TileWidth     = 78f;
    private const float TileHeight    = 98f;
    private const float TileSpacing   = 8f;
    private const float TileRadius    = 12f;
    private const float IconSize      = 60f;
    private const float CurrencySize  = 22f;
    private const float PriceFontSize = 20f;
    private const float PricePadding  = 5f;
    private const float MoreFontSize  = 28f;

    private const float MiniWidth      = 56f;
    private const float MiniMinWidth   = 40f;
    private const float MiniHeight     = 72f;
    private const float MiniIconSize   = 42f;
    private const float AmountFontSize = 18f;
    private const float LabelFontSize  = 22f;
    private const float LabelIconSize  = 28f;
    private const float LabelIconGap   = 4f;
    private const int   MaxBundleTiles = 10;

    // Body background is (0.941, 0.882, 0.784)
    private static readonly Color TileColor      = new(0.988f, 0.957f, 0.902f, 1f);
    private static readonly Color FallbackText   = new(0.267f, 0.325f, 0.478f, 1f);
    private static readonly Color CantAffordText = new(0.780f, 0.267f, 0.267f, 1f);

    private sealed class Tile
    {
        internal RectTransform Root = null!;
        internal Image Icon = null!;
        internal Image Currency = null!;
        internal TextMeshProUGUI Price = null!;
        internal TextMeshProUGUI More = null!;
    }

    private sealed class MiniTile
    {
        internal RectTransform Root = null!;
        internal Image Icon = null!;
        internal TextMeshProUGUI Amount = null!;
        internal TextMeshProUGUI More = null!;
    }

    private readonly record struct PendingIcon(Image Image, Func<bool> IsDone, Func<Sprite?> GetIcon);
    
    private static readonly Dictionary<IntPtr, PendingIcon> PendingIcons = new();

    private static readonly List<ShopRuntimeItem> LoadingItems = new();

    private static PreviewPopup? waitingPopup;
    private static List<ShopRuntimeItem>? waitingItems;

    internal static void Show(PreviewPopup popup, List<ShopRuntimeItem>? items)
    {
        StopWaiting();

        var layout = popup.transform.Find("Panel/Body/Content/Content Layout");

        if (!layout) return;

        var container = layout.Find(ContainerName);
        var maxShown  = Main.MaxShownItems;

        if (items == null || items.Count == 0 || maxShown <= 0)
        {
            var hidden = container && container.gameObject.activeSelf;

            if (hidden)
                container.gameObject.SetActive(false);

            if (HideBundleRows(layout, 0) || hidden)
                RebuildLayout(popup);

            return;
        }

        var mainText = layout.Find("MainText")?.GetComponent<TextMeshProUGUI>();

        container ??= CreateContainer(layout, ContainerName, TileHeight);
        container.SetSiblingIndex(mainText ? mainText!.transform.GetSiblingIndex() + 1 : layout.childCount - 1);

        var overflow  = items.Count > maxShown;
        var itemCount = overflow ? maxShown - 1 : items.Count;
        var tileCount = overflow ? maxShown : items.Count;

        var bundles = new List<(ShopRuntimeItem Item, ShopBundleDefinition Definition)>();

        for (var i = 0; i < tileCount; i++)
        {
            var tile = GetOrCreateTile(container, i, popup, mainText);

            if (i < itemCount)
            {
                var item = items[i];

                BindItem(tile, item, mainText);
                CollectBundle(item, bundles);
            }
            else
                BindMore(tile, items.Count - itemCount, mainText);

            tile.Root.gameObject.SetActive(true);
        }

        for (var i = tileCount; i < container.childCount; i++)
            container.GetChild(i).gameObject.SetActive(false);

        container.gameObject.SetActive(true);

        ShowBundleRows(layout, container, popup, bundles, mainText);

        if (LoadingItems.Count > 0)
        {
            waitingPopup = popup;
            waitingItems = items;
        }

        RebuildLayout(popup);
    }

    internal static void Tick()
    {
        TickWaiting();

        if (PendingIcons.Count == 0) return;

        List<IntPtr>? done = null;

        foreach (var (key, pending) in PendingIcons)
        {
            if (!pending.Image)
            {
                (done ??= new()).Add(key);
                continue;
            }

            if (!pending.IsDone()) continue;

            SetIcon(pending.Image, pending.GetIcon());
            (done ??= new()).Add(key);
        }

        if (done == null) return;

        foreach (var key in done)
            PendingIcons.Remove(key);
    }

    private static void TickWaiting()
    {
        if (waitingPopup == null) return;

        if (!waitingPopup)
        {
            StopWaiting();
            return;
        }

        foreach (var item in LoadingItems)
        {
            if (!item.IsDoneLoading && !item.IsLoadFailure) return;
        }

        var popup = waitingPopup;
        var items = waitingItems;

        StopWaiting();
        Show(popup, items);
    }

    private static void StopWaiting()
    {
        waitingPopup = null;
        waitingItems = null;
        LoadingItems.Clear();
    }

    private static void CollectBundle(ShopRuntimeItem item, List<(ShopRuntimeItem Item, ShopBundleDefinition Definition)> bundles)
    {
        if (!item.IsDoneLoading)
        {
            if (!item.IsLoadFailure)
                LoadingItems.Add(item);

            return;
        }

        var definition = item.Asset?.TryCast<ShopBundleDefinition>();

        if (definition != null && definition.BundledItems != null && definition.BundledItems.Count > 0)
            bundles.Add((item, definition));
    }

    private static void ShowBundleRows(Transform layout, Transform after, PreviewPopup popup, List<(ShopRuntimeItem Item, ShopBundleDefinition Definition)> bundles, TextMeshProUGUI? mainText)
    {
        var siblingIndex = after.GetSiblingIndex();
        var width        = AvailableWidth(layout, after);

        for (var b = 0; b < bundles.Count; b++)
        {
            var row = layout.Find(BundlePrefix + b) ?? CreateContainer(layout, BundlePrefix + b, MiniHeight);

            row.SetSiblingIndex(++siblingIndex);
            FillBundleRow(row, popup, bundles[b].Item, bundles[b].Definition, mainText, width);
            row.gameObject.SetActive(true);
        }

        HideBundleRows(layout, bundles.Count);
    }

    private static bool HideBundleRows(Transform layout, int from)
    {
        var hidden = false;
        var row    = layout.Find(BundlePrefix + from);

        while (row)
        {
            if (row.gameObject.activeSelf)
            {
                row.gameObject.SetActive(false);
                hidden = true;
            }

            row = layout.Find(BundlePrefix + ++from);
        }

        return hidden;
    }

    private static void FillBundleRow(Transform row, PreviewPopup popup, ShopRuntimeItem item, ShopBundleDefinition definition, TextMeshProUGUI? mainText, float width)
    {
        var contents = definition.BundledItems;
        var count    = contents.Count;

        var overflow     = count > MaxBundleTiles;
        var contentCount = overflow ? MaxBundleTiles - 1 : count;
        var tileCount    = overflow ? MaxBundleTiles : count;

        var label      = GetOrCreateLabel(row, mainText);
        var labelIcon  = label.transform.Find("Icon").GetComponent<Image>();
        var bundleName = LocalizedText(item) ?? LocalizedText(definition);

        label.text     = bundleName != null ? bundleName + ":" : string.Empty;
        label.fontSize = LabelFontSize;

        var iconSpace = LabelIconSize + LabelIconGap;
        label.margin = Vector4.zero;

        var labelWidth = label.text.Length > 0 ? Mathf.Ceil(label.GetPreferredValues(label.text).x) + iconSpace + 2f : 0f;

        label.margin = new Vector4(iconSpace, 0f, 0f, 0f);

        BindIcon(labelIcon, () => item.IsDoneLoading || item.IsLoadFailure, () => (item.IsLoadFailure ? null : item.Icon) ?? definition.BundleIcon);

        width -= 2f;

        var miniWidth   = MiniWidth;
        var ownLine     = false;

        if (width > 0f)
        {
            var sameLine = (width - labelWidth - TileSpacing * tileCount) / tileCount;

            if (labelWidth > 0f && sameLine < MiniMinWidth)
            {
                ownLine  = true;
                sameLine = (width - TileSpacing * (tileCount - 1)) / tileCount;
            }

            miniWidth = Mathf.Clamp(sameLine, MiniMinWidth, MiniWidth);
        }

        var scale      = miniWidth / MiniWidth;
        var miniHeight = MiniHeight * scale;

        label.rectTransform.sizeDelta = ownLine
            ? new Vector2(width, LabelFontSize + 6f)
            : new Vector2(labelWidth, miniHeight);
        label.gameObject.SetActive(labelWidth > 0f);

        for (var i = 0; i < tileCount; i++)
        {
            var tile = GetOrCreateMiniTile(row, i, popup, mainText);

            LayoutMiniTile(tile, miniWidth, miniHeight, scale);

            if (i < contentCount)
                BindBundleContent(tile, contents[i], mainText);
            else
                BindMiniMore(tile, count - contentCount, mainText);

            tile.Root.gameObject.SetActive(true);
        }

        for (var i = tileCount + 1; i < row.childCount; i++)
            row.GetChild(i).gameObject.SetActive(false);
    }

    private static float AvailableWidth(Transform layout, Transform itemContainer)
    {
        var width = itemContainer.Cast<RectTransform>().rect.width;

        if (width <= 0f)
            width = layout.Cast<RectTransform>().rect.width;

        return width;
    }

    private static string? LocalizedText(ShopRuntimeItem item)
    {
        try
        {
            var text = item.Title?.GetLocalizedString();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch
        {
            return null;
        }
    }

    private static string? LocalizedText(ShopBundleDefinition definition)
    {
        try
        {
            var text = definition.BundleName?.GetLocalizedString();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch
        {
            return null;
        }
    }

    private static Transform CreateContainer(Transform layout, string name, float height)
    {
        var obj = new GameObject(name) { layer = layout.gameObject.layer };

        var rect = obj.AddComponent<RectTransform>();
        rect.SetParent(layout, false);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot     = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(0f, height);

        var wrap = obj.AddComponent<HorizontalWrappingLayout>();
        wrap._justification = Justification.START;
        wrap.Spacing        = new Vector2(TileSpacing, TileSpacing);

        var fit = obj.AddComponent<EncapsulateChildren>();
        fit._vertical = true;

        return rect;
    }

    private static Tile GetOrCreateTile(Transform container, int index, PreviewPopup popup, TextMeshProUGUI? mainText)
    {
        if (index < container.childCount)
            return ReadTile(container.GetChild(index));

        var root = NewRect(TilePrefix + index, container);
        root.sizeDelta = new Vector2(TileWidth, TileHeight);

        CreateBackground(root, popup);

        var icon = NewRect("Icon", root).gameObject.AddComponent<Image>();
        var iconRect = icon.rectTransform;
        iconRect.anchorMin        = new Vector2(0.5f, 1f);
        iconRect.anchorMax        = new Vector2(0.5f, 1f);
        iconRect.pivot            = new Vector2(0.5f, 1f);
        iconRect.anchoredPosition = new Vector2(0f, -6f);
        iconRect.sizeDelta        = new Vector2(IconSize, IconSize);
        icon.preserveAspect = true;
        icon.raycastTarget  = false;

        var currency = NewRect("Currency", root).gameObject.AddComponent<Image>();
        var currencyRect = currency.rectTransform;
        currencyRect.anchorMin = new Vector2(0.5f, 0f);
        currencyRect.anchorMax = new Vector2(0.5f, 0f);
        currencyRect.pivot     = new Vector2(0f, 0f);
        currencyRect.sizeDelta = new Vector2(CurrencySize, CurrencySize);
        currency.preserveAspect = true;
        currency.raycastTarget  = false;

        var price = CreateText("Price", root, mainText, PriceFontSize, TextAlignmentOptions.MidlineLeft);
        var priceRect = price.rectTransform;
        priceRect.anchorMin = new Vector2(0.5f, 0f);
        priceRect.anchorMax = new Vector2(0.5f, 0f);
        priceRect.pivot     = new Vector2(0f, 0f);
        priceRect.sizeDelta = new Vector2(TileWidth, CurrencySize);

        var more = CreateText("More", root, mainText, MoreFontSize, TextAlignmentOptions.Center);
        Stretch(more.rectTransform);

        return ReadTile(root);
    }

    private static Tile ReadTile(Transform root) => new()
    {
        Root     = root.Cast<RectTransform>(),
        Icon     = root.Find("Icon").GetComponent<Image>(),
        Currency = root.Find("Currency").GetComponent<Image>(),
        Price    = root.Find("Price").GetComponent<TextMeshProUGUI>(),
        More     = root.Find("More").GetComponent<TextMeshProUGUI>()
    };

    private static TextMeshProUGUI GetOrCreateLabel(Transform row, TextMeshProUGUI? mainText)
    {
        if (row.childCount > 0)
            return row.GetChild(0).GetComponent<TextMeshProUGUI>();

        var label = CreateText(LabelName, row, mainText, LabelFontSize, TextAlignmentOptions.MidlineLeft);
        label.fontStyle |= FontStyles.Bold;

        var icon = NewRect("Icon", label.transform).gameObject.AddComponent<Image>();
        var iconRect = icon.rectTransform;
        iconRect.anchorMin        = new Vector2(0f, 0.5f);
        iconRect.anchorMax        = new Vector2(0f, 0.5f);
        iconRect.pivot            = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta        = new Vector2(LabelIconSize, LabelIconSize);
        icon.preserveAspect = true;
        icon.raycastTarget  = false;

        return label;
    }

    private static MiniTile GetOrCreateMiniTile(Transform row, int index, PreviewPopup popup, TextMeshProUGUI? mainText)
    {
        if (index + 1 < row.childCount)
            return ReadMiniTile(row.GetChild(index + 1));

        var root = NewRect(TilePrefix + index, row);

        CreateBackground(root, popup);

        var icon = NewRect("Icon", root).gameObject.AddComponent<Image>();
        var iconRect = icon.rectTransform;
        iconRect.anchorMin = new Vector2(0.5f, 1f);
        iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot     = new Vector2(0.5f, 1f);
        icon.preserveAspect = true;
        icon.raycastTarget  = false;

        var amount = CreateText("Amount", root, mainText, AmountFontSize, TextAlignmentOptions.Bottom);
        var amountRect = amount.rectTransform;
        amountRect.anchorMin = new Vector2(0f, 0f);
        amountRect.anchorMax = new Vector2(1f, 0f);
        amountRect.pivot     = new Vector2(0.5f, 0f);

        var more = CreateText("More", root, mainText, AmountFontSize, TextAlignmentOptions.Center);
        Stretch(more.rectTransform);

        return ReadMiniTile(root);
    }

    private static MiniTile ReadMiniTile(Transform root) => new()
    {
        Root   = root.Cast<RectTransform>(),
        Icon   = root.Find("Icon").GetComponent<Image>(),
        Amount = root.Find("Amount").GetComponent<TextMeshProUGUI>(),
        More   = root.Find("More").GetComponent<TextMeshProUGUI>()
    };

    private static void LayoutMiniTile(MiniTile tile, float width, float height, float scale)
    {
        tile.Root.sizeDelta = new Vector2(width, height);

        tile.Icon.rectTransform.anchoredPosition = new Vector2(0f, -4f * scale);
        tile.Icon.rectTransform.sizeDelta        = new Vector2(MiniIconSize * scale, MiniIconSize * scale);

        tile.Amount.fontSize = AmountFontSize * scale;
        tile.Amount.rectTransform.anchoredPosition = new Vector2(0f, 3f * scale);
        tile.Amount.rectTransform.sizeDelta        = new Vector2(0f, AmountFontSize * scale + 4f);

        tile.More.fontSize = MoreFontSize * scale * 0.8f;
    }

    private static void BindItem(Tile tile, ShopRuntimeItem item, TextMeshProUGUI? mainText)
    {
        tile.More.gameObject.SetActive(false);
        tile.Icon.gameObject.SetActive(true);

        BindIcon(tile.Icon, () => item.IsDoneLoading || item.IsLoadFailure, () => item.IsLoadFailure ? null : item.Icon);

        var cost     = FirstCurrencyCost(item.PurchaseCost);
        var amount   = cost?.Amount ?? 0;
        var currency = cost?.GetCurrencyOrDefault()?.TryCast<CurrencyDefinition>()
                       ?? CurrencyCostEntry.CreateNewbucks(0).GetCurrencyOrDefault()?.TryCast<CurrencyDefinition>();

        tile.Price.text  = amount.ToString();
        tile.Price.color = item.CanAfford(1) ? TextColor(mainText) : CantAffordText;

        var currencyIcon = currency?.Icon;
        tile.Currency.sprite = currencyIcon;
        tile.Currency.gameObject.SetActive(currencyIcon);
        tile.Price.gameObject.SetActive(true);

        var iconWidth = currencyIcon ? CurrencySize + 3f : 0f;

        tile.Price.fontSize = PriceFontSize;

        var textWidth = tile.Price.GetPreferredValues(tile.Price.text).x;
        var fitWidth  = TileWidth - PricePadding * 2f - iconWidth;

        if (textWidth > fitWidth && textWidth > 0f)
        {
            tile.Price.fontSize = PriceFontSize * fitWidth / textWidth;
            textWidth           = tile.Price.GetPreferredValues(tile.Price.text).x;
        }

        var left = -(iconWidth + textWidth) / 2f;

        tile.Currency.rectTransform.anchoredPosition = new Vector2(left, 7f);
        tile.Price.rectTransform.anchoredPosition    = new Vector2(left + iconWidth, 7f);
    }

    private static void BindMore(Tile tile, int hiddenCount, TextMeshProUGUI? mainText)
    {
        PendingIcons.Remove(tile.Icon.Pointer);

        tile.Icon.gameObject.SetActive(false);
        tile.Currency.gameObject.SetActive(false);
        tile.Price.gameObject.SetActive(false);

        tile.More.text  = "+" + hiddenCount;
        tile.More.color = TextColor(mainText);
        tile.More.gameObject.SetActive(true);
    }

    private static void BindBundleContent(MiniTile tile, ShopBundleDefinition.BundleItem content, TextMeshProUGUI? mainText)
    {
        tile.More.gameObject.SetActive(false);
        tile.Icon.gameObject.SetActive(true);
        tile.Amount.gameObject.SetActive(true);

        tile.Amount.text  = AmountText(tile.Amount, content.Amount);
        tile.Amount.color = TextColor(mainText);

        var asset = content.Item;

        if (asset == null || !asset.HasValidReference())
        {
            PendingIcons.Remove(tile.Icon.Pointer);
            SetIcon(tile.Icon, null);
            return;
        }

        if (!asset.IsAssetLoadingOrDone)
            asset.LoadAsset();

        BindIcon(tile.Icon, () => asset.IsDoneLoading, () => asset.LoadedInterface?.TryCast<IUIDisplayData>()?.Icon);
    }

    private static void BindMiniMore(MiniTile tile, int hiddenCount, TextMeshProUGUI? mainText)
    {
        PendingIcons.Remove(tile.Icon.Pointer);

        tile.Icon.gameObject.SetActive(false);
        tile.Amount.gameObject.SetActive(false);

        tile.More.text  = "+" + hiddenCount;
        tile.More.color = TextColor(mainText);
        tile.More.gameObject.SetActive(true);
    }

    private static void BindIcon(Image image, Func<bool> isDone, Func<Sprite?> getIcon)
    {
        PendingIcons.Remove(image.Pointer);

        if (isDone())
        {
            SetIcon(image, getIcon());
            return;
        }

        SetIcon(image, null);
        PendingIcons[image.Pointer] = new PendingIcon(image, isDone, getIcon);
    }

    private static string AmountText(TextMeshProUGUI text, int amount)
    {
        var font = text.font;
        var sign = font && font.HasCharacter('×', true, false) ? "×" : "x";

        return sign + amount;
    }

    private static CurrencyCostEntry? FirstCurrencyCost(PurchaseCost? cost)
    {
        var costs = cost?._currencyCosts;

        return costs != null && costs.Count > 0 ? costs[0] : null;
    }

    private static void SetIcon(Image image, Sprite? sprite)
    {
        image.sprite  = sprite;
        image.enabled = sprite;
    }

    private static void CreateBackground(RectTransform tile, PreviewPopup popup)
    {
        var source = popup.transform.Find("Drop Shadow")?.GetComponent<SDFRoundedRectangle>();

        if (!source)
        {
            var image = NewRect("Background", tile).gameObject.AddComponent<Image>();
            image.color         = TileColor;
            image.raycastTarget = false;
            Stretch(image.rectTransform);
            return;
        }

        var shape = Object.Instantiate(source!.gameObject, tile, false).GetComponent<SDFRoundedRectangle>();
        shape.name          = "Background";
        shape.color         = TileColor;
        shape.Blur          = 0f;
        shape.raycastTarget = false;
        shape.SetCornerRadius(TileRadius);
        Stretch(shape.rectTransform);
        shape.transform.SetAsFirstSibling();
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, TextMeshProUGUI? style, float size, TextAlignmentOptions alignment)
    {
        var text = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();

        if (style)
        {
            text.font               = style!.font;
            text.fontSharedMaterial = style.fontSharedMaterial;
            text.fontStyle          = style.fontStyle;
        }

        text.fontSize           = size;
        text.alignment          = alignment;
        text.enableWordWrapping = false;
        text.overflowMode       = TextOverflowModes.Overflow;
        text.raycastTarget      = false;
        text.color              = TextColor(style);

        return text;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var obj = new GameObject(name) { layer = parent.gameObject.layer };

        var rect = obj.AddComponent<RectTransform>();
        rect.SetParent(parent, false);

        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin        = Vector2.zero;
        rect.anchorMax        = Vector2.one;
        rect.pivot            = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta        = Vector2.zero;
    }

    private static Color TextColor(TextMeshProUGUI? style) => style ? style!.color : FallbackText;

    private static void RebuildLayout(PreviewPopup popup)
    {
        var manager = popup.GetComponentInParent<LayoutManager>(true);

        if (!manager) return;

        manager.ForceTreeRebuild();
        manager.ForceFullTreeLayout();
    }
}