using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class FarmPresentationPolish
{
    private const string Root = "Assets/_Project/";

    public static void Apply(Scene scene)
    {
        float groundTop = GameObject.Find("Ground").GetComponent<Renderer>().bounds.max.y;
        string path = Root + "Prefabs/FarmPlot.prefab";
        var prefab = PrefabUtility.LoadPrefabContents(path);
        try { RaiseSoil(prefab.GetComponent<FarmPlot>(), groundTop); PrefabUtility.SaveAsPrefabAsset(prefab, path); }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        foreach (var root in scene.GetRootGameObjects())
        foreach (var plot in root.GetComponentsInChildren<FarmPlot>(true)) RaiseSoil(plot, groundTop);
        FarmCropPrefabRefresh.Apply();

        var barn = GameObject.Find("FarmBarn");
        barn.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        barn.transform.localScale *= 6f / FarmCropPrefabRefresh.BoundsOf(barn).size.z;
        var bounds = FarmCropPrefabRefresh.BoundsOf(barn);
        var position = barn.transform.position;
        position.z = Mathf.Min(position.z, 10f - bounds.extents.z);
        barn.transform.position = position;
        PrefabUtility.RecordPrefabInstancePropertyModifications(barn.transform);
        PrefabUtility.ApplyPrefabInstance(barn, InteractionMode.AutomatedAction);
        FarmRefinementSetup.MoveBarnRight();

        var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Player.mat");
        if (material.HasProperty("_Tint")) material.SetColor("_Tint", new Color32(236, 181, 139, 255));
        EditorUtility.SetDirty(material);

        var canvas = GameObject.Find("Main Canvas").transform;
        var map = FarmMenuSetup.Find(canvas, "MapTab");
        FarmMenuSetup.Place((RectTransform)map, 1240f, -11f, 68f, 68f);
        var square = FarmMenuSetup.Sprite("PanelWoodRounded");
        FarmMenuSetup.Style(map.GetComponent<Image>(), square, 3f);
        var mapIcon = map.Find("IconMap").GetComponent<Image>();
        mapIcon.rectTransform.sizeDelta = new Vector2(54f, 54f);

        var fertilizer = FarmMenuSetup.Find(canvas, "IconFertilizer").GetComponent<Image>();
        fertilizer.rectTransform.sizeDelta = new Vector2(100f, 100f);
        fertilizer.rectTransform.anchoredPosition = new Vector2(-48f, 0f);
        foreach (var dropdown in canvas.GetComponentsInChildren<TMP_Dropdown>(true)) StyleDropdown(dropdown);
        foreach (var button in canvas.GetComponentsInChildren<Button>(true))
        {
            if (!button.name.StartsWith("Close")) continue;
            var rect = (RectTransform)button.transform;
            float right = rect.anchoredPosition.x + rect.sizeDelta.x;
            rect.sizeDelta = new Vector2(44f, 44f);
            rect.anchoredPosition = new Vector2(right - 44f, rect.anchoredPosition.y);
            button.GetComponent<Image>().sprite = FarmMenuSetup.Sprite("Button");
            button.GetComponent<Image>().type = Image.Type.Simple;
            button.GetComponent<Image>().preserveAspect = true;
        }
    }

    private static void RaiseSoil(FarmPlot plot, float groundTop)
    {
        var soil = plot.transform.Find("SoilMesh");
        float minimum = FarmPresentationSetup.MinimumSurfaceHeight(soil);
        soil.position += Vector3.up * Mathf.Max(0f, groundTop + 0.02f - minimum);
        var bounds = soil.GetComponent<Renderer>().bounds;
        var collider = plot.GetComponent<BoxCollider>();
        collider.center = plot.transform.InverseTransformPoint(bounds.center);
        collider.size = plot.transform.InverseTransformVector(bounds.size);
        PrefabUtility.RecordPrefabInstancePropertyModifications(soil);
        PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
    }

    private static void StyleDropdown(TMP_Dropdown dropdown)
    {
        var panel = FarmMenuSetup.Sprite("PanelWoodRounded");
        var wood = FarmMenuSetup.Sprite("ButtonWood");
        FarmMenuSetup.Style(dropdown.GetComponent<Image>(), wood, 4f);
        var background = dropdown.template.GetComponent<Image>();
        if (background != null) FarmMenuSetup.Style(background, panel, 3f);
        foreach (var image in dropdown.template.GetComponentsInChildren<Image>(true))
        {
            if (image.name.Contains("Background"))
            {
                FarmMenuSetup.Style(image, wood, 4f);
                image.color = new Color32(255, 246, 218, 255);
            }
            else if (image.name == "Handle") FarmMenuSetup.Style(image, FarmMenuSetup.Sprite("ButtonWoodGreen"), 4f);
        }
        foreach (var text in dropdown.template.GetComponentsInChildren<TMP_Text>(true)) text.color = FarmMenuSetup.Ink;
        var arrow = dropdown.transform.Find("Arrow");
        if (arrow != null)
        {
            var image = arrow.GetComponent<Image>();
            image.sprite = FarmMenuSetup.Sprite("IconBack");
            image.color = Color.white;
            image.preserveAspect = true;
            arrow.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }
    }
}
