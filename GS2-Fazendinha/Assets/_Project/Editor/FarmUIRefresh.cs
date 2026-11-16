using System;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class FarmUIRefresh
{
    private const string Root = "Assets/_Project/";
    private static readonly Color Ink = new Color32(76, 43, 24, 255);
    private static readonly StringBuilder Report = new StringBuilder();

    public static void NormalizeButtonSprites()
    {
        foreach (var name in new[] { "Button", "Button02", "Button03", "PlayButton", "PauseButton" })
            Prepare(name, name);
    }

    public static void Apply(Scene scene)
    {
        Report.Clear();
        var wood = Prepare("ButtonV2(5)", "ButtonWood", true);
        var panel = Prepare("ButtonV2(3)", "PanelWood", true);
        Prepare("ButtonV2(2)", "PanelWoodRounded", true);
        Prepare("ButtonV2(4)", "PanelWoodDecorated", true);
        Prepare("ButtonV2(6)", "ButtonWoodGreen", true);
        PrepareSheet("ButtonV2(1)", true);
        PrepareSheet("ButtonV3(1)", false);
        foreach (var name in new[] { "Button", "Button02", "Button03", "ButtonV3(2)", "ButtonV3(3)", "ButtonV3(4)" })
            Prepare(name, name, name.StartsWith("ButtonV3"));
        foreach (var name in new[] { "ButtonSair", "PauseButton", "PlayButton" }) Prepare(name, name);

        var money = Prepare("Dinheiro", "IconMoney");
        var coin = Prepare("Moeda", "IconCoin");
        var water = Prepare("Agua(2)", "IconWater");
        var retention = Prepare("Agua(1)", "IconHydrationRetention");
        var fertilizer = Prepare("Adubo", "IconFertilizer");
        var worms = Prepare("Minhocas", "IconWormComposter");
        var tank = Prepare("GalaoDeAgua", "IconWaterTank");
        var collector = Prepare("Torneira(1)", "IconWaterCollection");
        var irrigation = Prepare("irrigador", "IconIrrigation");
        var pipes = Prepare("Canos(2)", "IconPipes");
        var map = Prepare("Mapa", "IconMap");
        var market = Prepare("MarketButton", "IconMarket");
        var back = Prepare("BackButton", "IconBack");
        var close = Prepare("CloseButton", "IconClose");

        string[] crops = { "Carrot", "Corn", "Lettuce", "Wheat", "Sunflower" };
        string[] sources = { "Cenoura", "Milho]", "Alface", "Trigo", "Girassol" };
        for (int index = 0; index < crops.Length; index++)
        {
            var data = AssetDatabase.LoadAssetAtPath<CropData>(Root + "Crops/" + crops[index] + ".asset");
            data.icon = Prepare(sources[index], "Icon" + crops[index]);
            EditorUtility.SetDirty(data);
        }
        foreach (var guid in AssetDatabase.FindAssets("t:TechnologyData", new[] { Root + "Tech" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var data = AssetDatabase.LoadAssetAtPath<TechnologyData>(path);
            if (data.effect == TechnologyEffect.ShowGrowingTime) continue;
            Sprite icon = null;
            if (path.Contains("Reinforced Pipes")) icon = pipes;
            else if (path.Contains("Soil") || path.Contains("Fert")) icon = fertilizer;
            else if (data.effect == TechnologyEffect.WormComposter) icon = worms;
            else if (data.effect == TechnologyEffect.TankCapacityBonus) icon = tank;
            else if (data.effect == TechnologyEffect.WaterCollectionBonus) icon = collector;
            else if (data.effect == TechnologyEffect.WaterCostReduction) icon = irrigation;
            else if (data.effect == TechnologyEffect.HydrationRetention) icon = retention;
            if (icon == null) continue;
            data.icon = icon;
            EditorUtility.SetDirty(data);
            Report.AppendLine(data.DisplayName + " -> " + icon.name);
        }

        var canvas = GameObject.Find("Main Canvas").transform;
        foreach (var image in canvas.GetComponentsInChildren<Image>(true))
        {
            if (image.name.EndsWith("Panel")) Style(image, panel, 1.5f);
            else if (image.GetComponent<Button>() != null || image.GetComponent<TMP_Dropdown>() != null)
                Style(image, wood, 4f);
            else if (image.name.EndsWith("IconFrame"))
            {
                Style(image, panel, 3f);
                image.color = new Color(1f, 1f, 1f, 0.35f);
            }
        }
        foreach (var button in canvas.GetComponentsInChildren<Button>(true))
        {
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.96f, 0.82f);
            colors.pressedColor = new Color(0.83f, 0.72f, 0.52f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            button.colors = colors;
        }
        foreach (var label in canvas.GetComponentsInChildren<TMP_Text>(true))
        {
            for (Transform parent = label.transform; parent != canvas && parent != null; parent = parent.parent)
                if (parent.name.EndsWith("Panel") || parent.GetComponent<Button>() != null || parent.GetComponent<TMP_Dropdown>() != null)
                { label.color = Ink; break; }
        }

        var shop = Find(canvas, "ShopPanel");
        AddIcon(Find(shop, "SellProduceButton"), "IconCoin", coin, new Vector2(28f, 0f), new Vector2(30f, 30f), true);
        var counter = Find(canvas, "CoinCounter");
        Style(counter.GetComponent<Image>(), wood, 4f);
        AddIcon(counter, "IconMoney", money, new Vector2(31f, 0f), new Vector2(44f, 38f), true);
        var coinText = Find(counter, "CoinCounterText").GetComponent<TMP_Text>();
        coinText.rectTransform.anchorMin = Vector2.zero;
        coinText.rectTransform.anchorMax = Vector2.one;
        coinText.rectTransform.offsetMin = new Vector2(60f, 0f);
        coinText.rectTransform.offsetMax = new Vector2(-8f, 0f);
        coinText.enableAutoSizing = true;
        coinText.fontSizeMin = 14f;
        coinText.fontSizeMax = 24f;
        coinText.enableWordWrapping = false;
        coinText.alignment = TextAlignmentOptions.MidlineLeft;
        coinText.color = Ink;

        var oldWater = Find(canvas, "WaterIcon").GetComponent<Image>();
        oldWater.sprite = water;
        oldWater.color = Color.white;
        oldWater.preserveAspect = true;
        oldWater.raycastTarget = false;
        var fertilizerSlider = Find(canvas, "FertilizerSlider");
        var previousFertilizerIcon = canvas.GetComponentsInChildren<Image>(true).FirstOrDefault(item => item.name == "IconFertilizer");
        if (previousFertilizerIcon != null) previousFertilizerIcon.transform.SetParent(fertilizerSlider, false);
        AddIcon(fertilizerSlider, "IconFertilizer", fertilizer, new Vector2(-28f, 0f), new Vector2(72f, 72f), true);

        IconButton(Find(canvas, "MapTab"), map, "IconMap", new Vector2(52f, 42f));
        IconButton(Find(canvas, "MarketMapButton"), market, "IconMarket", new Vector2(48f, 42f), false);
        foreach (var button in canvas.GetComponentsInChildren<Button>(true))
        {
            if (button.name.StartsWith("Close")) IconButton(button.transform, close, "IconClose", new Vector2(26f, 26f));
            else if (button.name.StartsWith("Back"))
                AddIcon(button.transform, "IconBack", back, new Vector2(24f, 0f), new Vector2(28f, 22f), true);
        }
        File.WriteAllText("../../Verification/AssetRefresh/UI.txt", Report.ToString());
    }

    private static void IconButton(Transform button, Sprite sprite, string name, Vector2 size, bool hideLabel = true)
    {
        if (hideLabel)
            foreach (var label in button.GetComponentsInChildren<TMP_Text>(true)) label.gameObject.SetActive(false);
        var icon = AddIcon(button, name, sprite, Vector2.zero, size, false);
        icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        icon.rectTransform.anchoredPosition = hideLabel ? Vector2.zero : new Vector2(-100f, 0f);
        if (!hideLabel)
        {
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            icon.rectTransform.anchoredPosition = new Vector2(36f, 0f);
            foreach (var label in button.GetComponentsInChildren<TMP_Text>(true))
            {
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = new Vector2(72f, 8f);
                label.rectTransform.offsetMax = new Vector2(-12f, -8f);
            }
        }
    }

    private static Image AddIcon(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size, bool left)
    {
        var existing = parent.Find(name);
        var image = existing != null ? existing.GetComponent<Image>() :
            new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(parent, false);
        image.gameObject.layer = 5;
        var rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(left ? 0f : 0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        image.sprite = sprite;
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private static Transform Find(Transform root, string name) =>
        root.GetComponentsInChildren<Transform>(true).First(item => item.name == name);

    private static void Style(Image image, Sprite sprite, float multiplier)
    {
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = multiplier;
        image.color = Color.white;
    }

    private static Sprite Prepare(string original, string name, bool border = false)
    {
        string path = Root + "UI/" + name + ".png";
        if (!File.Exists(path))
        {
            string source = Root + "UI/" + original + ".png";
            string error = AssetDatabase.MoveAsset(source, path);
            if (!string.IsNullOrEmpty(error)) throw new Exception(error);
        }
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Bilinear;
        var slices = importer.spritesheet;
        if (slices.Length == 0)
            slices = new[] { new SpriteMetaData { name = name, rect = AlphaRect(path), pivot = new Vector2(0.5f, 0.5f), alignment = 0 } };
        if (slices.Length == 1) slices[0].rect = AlphaRect(path);
        for (int index = 0; index < slices.Length; index++)
        {
            slices[index].name = name;
            if (border)
            {
                float edge = Mathf.Min(slices[index].rect.width, slices[index].rect.height) * (name.StartsWith("Panel") ? 0.32f : 0.24f);
                slices[index].border = new Vector4(edge, edge, edge, edge);
            }
        }
        importer.spritesheet = slices;
        importer.SaveAndReimport();
        var sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().First();
        Report.AppendLine(path + " -> " + sprite.name + "; border=" + sprite.border);
        return sprite;
    }

    // Usa a área visível do PNG, sem recortar ou alterar a imagem original.
    private static Rect AlphaRect(string path)
    {
        var texture = new Texture2D(2, 2);
        texture.LoadImage(File.ReadAllBytes(path));
        var pixels = texture.GetPixels32();
        int left = texture.width, right = 0, bottom = texture.height, top = 0;
        for (int y = 0; y < texture.height; y++)
        for (int x = 0; x < texture.width; x++)
            if (pixels[y * texture.width + x].a > 8)
            { left = Mathf.Min(left, x); right = Mathf.Max(right, x); bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y); }
        var rect = new Rect(left, bottom, right - left + 1, top - bottom + 1);
        UnityEngine.Object.DestroyImmediate(texture);
        return rect;
    }

    private static void PrepareSheet(string name, bool border)
    {
        string path = Root + "UI/" + name + ".png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer.spritesheet.Length == 0 && name == "ButtonV3(1)")
        {
            var rect = AlphaRect(path);
            float height = rect.height / 3f;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritesheet = Enumerable.Range(0, 3).Select(index => new SpriteMetaData
            {
                name = "ButtonRounded" + index,
                rect = new Rect(rect.x, rect.y + index * height, rect.width, height),
                pivot = new Vector2(0.5f, 0.5f), alignment = 0
            }).ToArray();
        }
        var slices = importer.spritesheet;
        for (int index = 0; index < slices.Length; index++)
        {
            float edge = Mathf.Min(slices[index].rect.width, slices[index].rect.height) * (border ? 0.12f : 0.2f);
            slices[index].border = new Vector4(edge, edge, edge, edge);
        }
        importer.spritesheet = slices;
        importer.SaveAndReimport();
    }
}
