using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FarmAssetRefresh
{
    private const string Root = "Assets/_Project/";
    private const string Reports = "../../Verification/AssetRefresh/";

    public static void FinishUI()
    {
        var scene = EditorSceneManager.OpenScene(Root + "Scenes/MainLevel.unity");
        FarmUIRefresh.Apply(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        FarmSystemChecks.RunAssetRefresh();
    }

    public static void Apply()
    {
        Directory.CreateDirectory(Reports);
        var scene = EditorSceneManager.OpenScene(Root + "Scenes/MainLevel.unity");
        FarmBuildingRefresh.Apply(scene);
        FarmCropPrefabRefresh.Apply();
        FarmUIRefresh.Apply(scene);
        var tank = Object.FindFirstObjectByType<FarmWaterTank>();
        var tankData = new SerializedObject(tank);
        tankData.FindProperty("regenerationInterval").floatValue = 3.5f;
        tankData.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        FarmSystemChecks.RunAssetRefresh();
    }

    public static void Inspect()
    {
        Directory.CreateDirectory(Reports);
        EditorSceneManager.OpenScene(Root + "Scenes/MainLevel.unity");
        var preview = EditorSceneManager.NewPreviewScene();
        var report = new StringBuilder();
        foreach (var folder in new[] { "Models", "Prefabs/Seed", "Prefabs/Growing", "Prefabs/Grown" })
        foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new[] { Root + folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, preview);
            instance.transform.position = Vector3.zero;
            report.AppendLine(path + " | scale=" + instance.transform.localScale.ToString("F4") +
                " | children=" + instance.transform.childCount);
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                report.AppendLine("  bounds center=" + bounds.center.ToString("F4") + " size=" + bounds.size.ToString("F4"));
            }
            foreach (Transform child in instance.transform)
                report.AppendLine("  child=" + child.name + " position=" + child.localPosition.ToString("F4") +
                    " rotation=" + child.localEulerAngles.ToString("F2") + " scale=" + child.localScale.ToString("F4"));
            foreach (var renderer in renderers)
            foreach (var material in renderer.sharedMaterials)
                report.AppendLine("  material=" + (material != null ? material.name + " shader=" + material.shader.name +
                    " texture=" + (material.mainTexture != null ? material.mainTexture.name : "NONE") : "MISSING"));
            Object.DestroyImmediate(instance);
        }
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "UI" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            report.AppendLine("SPRITES " + path);
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Sprite sprite)
                    report.AppendLine("  " + sprite.name + " rect=" + sprite.rect + " border=" + sprite.border);
        }
        EditorSceneManager.ClosePreviewScene(preview);
        File.WriteAllText(Reports + "Inspection.txt", report.ToString());
    }
}
