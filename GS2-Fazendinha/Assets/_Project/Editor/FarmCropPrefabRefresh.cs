using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class FarmCropPrefabRefresh
{
    private const string Root = "Assets/_Project/";

    public static void Apply()
    {
        var report = new StringBuilder();
        var plot = UnityEngine.Object.FindFirstObjectByType<FarmPlot>();
        var anchor = (Transform)new SerializedObject(plot).FindProperty("cropVisualAnchor").objectReferenceValue;
        float surface = anchor.InverseTransformPoint(plot.transform.Find("SoilMesh").GetComponent<Renderer>().bounds.max).y;
        foreach (string name in new[] { "Carrot", "Corn", "Lettuce", "Wheat", "Sunflower" })
        {
            var crop = AssetDatabase.LoadAssetAtPath<CropData>(Root + "Crops/" + name + ".asset");
            crop.plantedPrefab = Rebuild(crop.plantedPrefab, name, "Seed", surface, report);
            crop.growingPrefab = Rebuild(crop.growingPrefab, name, "Growing", surface, report);
            crop.grownPrefab = Rebuild(crop.grownPrefab, name, "Grown", surface, report);
            AlignDead(crop.deadPrefab, surface);
            EditorUtility.SetDirty(crop);
        }
        File.WriteAllText("../../Verification/AssetRefresh/CropPrefabs.txt", report.ToString());
    }

    private static void AlignDead(GameObject prefab, float surface)
    {
        string path = AssetDatabase.GetAssetPath(prefab);
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            root.transform.position += Vector3.up * (surface - BoundsOf(root).min.y);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static GameObject Rebuild(GameObject prefab, string crop, string stage, float surface, StringBuilder report)
    {
        string path = AssetDatabase.GetAssetPath(prefab);
        var root = PrefabUtility.LoadPrefabContents(path);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Models/Crops/" + stage + "/SM_Crop_" + crop + "_Stage_" + stage + ".fbx");
        if (model == null) throw new Exception("Missing crop model: " + crop + " " + stage);
        try
        {
            var children = new Transform[root.transform.childCount];
            for (int index = 0; index < children.Length; index++) children[index] = root.transform.GetChild(index);
            for (int index = 0; index < children.Length; index++)
            {
                var previous = children[index];
                var plant = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                plant.name = "SM_Crop_" + crop + "_Stage_" + stage + "_" + (index + 1).ToString("00");
                plant.transform.localPosition = previous.localPosition;
                plant.transform.localRotation = previous.localRotation;
                plant.transform.localScale = previous.localScale;
                UnityEngine.Object.DestroyImmediate(previous.gameObject);
                var bounds = BoundsOf(plant);
                plant.transform.position += Vector3.up * (surface - bounds.min.y);
                PrefabUtility.RecordPrefabInstancePropertyModifications(plant.transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(plant);
            }
            root.transform.localPosition = Vector3.zero;
            PrefabUtility.SaveAsPrefabAsset(root, path);
            var groupBounds = BoundsOf(root);
            report.AppendLine(crop + " " + stage + ": " + children.Length + " plants; bounds=" + groupBounds.size.ToString("F3"));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    public static Bounds BoundsOf(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) throw new Exception("No crop renderers: " + root.name);
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }
}
