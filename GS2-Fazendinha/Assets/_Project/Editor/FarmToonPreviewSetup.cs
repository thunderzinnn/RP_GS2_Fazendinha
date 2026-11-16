using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FarmToonPreviewSetup
{
    private const string Root = "Assets/_Project/";
    private const string Folder = Root + "Materials/ToonPreview";

    public static void AlignCarrotBases()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(Root + "Scenes/MainLevel.unity");
        var plot = Object.FindFirstObjectByType<FarmPlot>();
        var anchor = (Transform)new SerializedObject(plot).FindProperty("cropVisualAnchor").objectReferenceValue;
        var soil = plot.transform.Find("SoilMesh").GetComponent<Renderer>();
        float surface = anchor.InverseTransformPoint(new Vector3(anchor.position.x, soil.bounds.max.y, anchor.position.z)).y;
        foreach (string guid in AssetDatabase.FindAssets("t:CropData", new[] { Root }))
        {
            var crop = AssetDatabase.LoadAssetAtPath<CropData>(AssetDatabase.GUIDToAssetPath(guid));
            if (crop == null || crop.CropName != "Carrot") continue;
            foreach (var prefab in new[] { crop.plantedPrefab, crop.growingPrefab, crop.grownPrefab })
            {
                string path = AssetDatabase.GetAssetPath(prefab);
                var model = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    float correction = surface - FarmCropPrefabRefresh.BoundsOf(model).min.y;
                    foreach (Transform child in model.transform)
                    {
                        child.position += Vector3.up * correction;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(child);
                    }
                    PrefabUtility.SaveAsPrefabAsset(model, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(model); }
            }
        }
        AssetDatabase.SaveAssets();
    }

    public static void CheckCropAlignment()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(Root + "Scenes/MainLevel.unity");
        var report = new System.Text.StringBuilder();
        foreach (var plot in Object.FindObjectsByType<FarmPlot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var anchor = (Transform)new SerializedObject(plot).FindProperty("cropVisualAnchor").objectReferenceValue;
            var soil = plot.transform.Find("SoilMesh").GetComponent<Renderer>();
            report.AppendLine(plot.name + " soilTop=" + soil.bounds.max.y + " enabled=" + soil.enabled + " anchor=" + anchor.position);
            foreach (string guid in AssetDatabase.FindAssets("t:CropData", new[] { Root }))
            {
                var crop = AssetDatabase.LoadAssetAtPath<CropData>(AssetDatabase.GUIDToAssetPath(guid));
                if (crop == null || crop.plantedPrefab == null) continue;
                var model = Object.Instantiate(crop.plantedPrefab, anchor, false);
                foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                    report.AppendLine(crop.name + " seed bottom=" + renderer.bounds.min.y + " delta=" + (renderer.bounds.min.y - soil.bounds.max.y));
                Object.DestroyImmediate(model);
            }
        }
        File.WriteAllText("../../Verification/DeliveryPolish/CropAlignment.txt", report.ToString());
    }

    public static void ApplyBarnPreview()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(Root + "Scenes/MainLevel.unity");
        var barn = GameObject.Find("FarmBarn");
        if (barn == null) throw new System.InvalidOperationException("FarmBarn was not found.");
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(Root + "Shaders/FarmToonPreview.shadergraph");
        foreach (var renderer in barn.GetComponentsInChildren<Renderer>(true))
        {
            renderer.sharedMaterials = renderer.sharedMaterials.Select(source => Create(source, shader)).ToArray();
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            EditorUtility.SetDirty(renderer);
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    public static void CreateMaterials()
    {
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(Root + "Shaders/FarmToonPreview.shadergraph");
        if (shader == null) throw new System.InvalidOperationException("Toon preview shader was not imported.");
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { Root }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Skip(path)) continue;
            foreach (var material in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>()) Create(material, shader);
        }
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Root + "Models" }))
            foreach (var material in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)).OfType<Material>()) Create(material, shader);
        AssetDatabase.SaveAssets();
    }

    public static void ApplyToScene(Scene scene)
    {
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(Root + "Shaders/FarmToonPreview.shadergraph");
        foreach (var root in scene.GetRootGameObjects())
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.GetComponentInParent<Canvas>(true) != null || renderer.GetComponentInParent<ParticleSystem>(true) != null) continue;
            var materials = renderer.sharedMaterials;
            bool changed = false;
            for (int index = 0; index < materials.Length; index++)
            {
                var source = materials[index];
                if (source == null || Skip(AssetDatabase.GetAssetPath(source)) || source.shader == shader) continue;
                materials[index] = Create(source, shader);
                changed = true;
            }
            if (changed) renderer.sharedMaterials = materials;
        }
    }

    private static Material Create(Material source, Shader shader)
    {
        if (source == null || source.shader == shader || source.shader.name.Contains("TextMeshPro") || source.shader.name.StartsWith("UI/")) return source;
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string guid, out long id);
        string name = source.name.Replace('/', '_').Replace('\\', '_');
        string path = Folder + "/" + name + "_" + guid.Substring(0, Mathf.Min(8, guid.Length)) + "_" + id + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.shaderKeywords = System.Array.Empty<string>();
        Color tint = source.HasProperty("_Tint") ? source.GetColor("_Tint") :
            source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
        material.SetColor("_Tint", tint);
        material.SetFloat("_AmbientIntensity", 0.14f);
        string texture = source.HasProperty("_BaseMap") ? "_BaseMap" : source.HasProperty("_MainTex") ? "_MainTex" : null;
        material.SetTexture("_BaseMap", texture != null ? source.GetTexture(texture) : null);
        if (texture != null)
        {
            material.SetTextureScale("_BaseMap", source.GetTextureScale(texture));
            material.SetTextureOffset("_BaseMap", source.GetTextureOffset(texture));
        }
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static bool Skip(string path) => path.Contains("/ToonPreview/") || path.Contains("/Materials/Effects/") || path.Contains("/Materials/Menu/");
}
