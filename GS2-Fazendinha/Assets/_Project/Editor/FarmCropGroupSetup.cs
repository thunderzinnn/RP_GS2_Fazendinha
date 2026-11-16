using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FarmCropGroupSetup
{
    private const string Root = "Assets/_Project/";
    private static readonly string[] Names = { "Carrot", "Corn", "Lettuce", "Wheat", "Sunflower" };
    private static readonly int[] PlantCounts = { 4, 3, 9, 10, 5 };
    private static readonly int[] HarvestCounts = { 4, 9, 9, 10, 5 };
    private static readonly int[] SeedPrices = { 20, 36, 36, 50, 40 };
    private static readonly int[] SalePrices = { 10, 10, 12, 20, 25 };
    private static readonly float[] PlantedTimes = { 30f, 40f, 50f, 60f, 60f };
    private static readonly float[] GrowingTimes = { 40f, 80f, 40f, 85f, 90f };
    private static readonly float[] InitialHydration = { 12f, 10f, 40f, 2f, 6.6f };
    private static readonly float[] HydrationLoss = { 1.5f, 1.8f, 2f, 2f, 3.2f };
    private static readonly float[] WaterCosts = { 12f, 18f, 16f, 20f, 10f };
    private static readonly float[] WaterRecovery = { 37f, 45f, 34f, 50f, 35f };
    private static float surfaceHeight;
    private static readonly StringBuilder report = new StringBuilder();

    public static void Apply()
    {
        report.Clear();
        var scene = EditorSceneManager.OpenScene(Root + "Scenes/MainLevel.unity");
        var camera = Camera.main;
        Vector3 cameraRotation = camera.transform.localEulerAngles;
        Vector3 cameraScale = camera.transform.localScale;
        var originalTransforms = new Dictionary<Transform, string>();
        foreach (var root in scene.GetRootGameObjects())
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                originalTransforms[transform] = EditorJsonUtility.ToJson(transform);

        var firstPlot = UnityEngine.Object.FindFirstObjectByType<FarmPlot>();
        var soil = firstPlot.transform.Find("SoilMesh").GetComponent<Renderer>();
        var anchor = new SerializedObject(firstPlot).FindProperty("cropVisualAnchor").objectReferenceValue as Transform;
        surfaceHeight = (anchor != null ? anchor : firstPlot.transform).InverseTransformPoint(soil.bounds.max).y;
        EnsureFolder(Root + "Prefabs/Grown");

        var broccoli = Load<CropData>("Crops/Broccoli.asset");
        var cauliflower = Load<CropData>("Crops/Cauliflower.asset");
        var lettuce = Load<CropData>("Crops/Lettuce.asset");
        var wheat = Load<CropData>("Crops/Wheat.asset");
        lettuce.deadPrefab = broccoli.deadPrefab;
        wheat.deadPrefab = cauliflower.deadPrefab;

        for (int index = 0; index < Names.Length; index++)
        {
            string name = Names[index];
            var crop = Load<CropData>("Crops/" + name + ".asset");
            string seedPath = Root + "Prefabs/Seed/" + (name == "Carrot" ? "Crop" : "Crops") + "_" + name + "_Seed_Group.prefab";
            string growingPath = Root + "Prefabs/Growing/Crops_" + name + "_Growing_Group.prefab";
            string grownPath = Root + "Prefabs/Grown/Crops_" + name + "_Grown_Group.prefab";

            if (name == "Wheat" && AssetDatabase.LoadAssetAtPath<GameObject>(seedPath) == null)
                CreateWheatSeed(seedPath);
            NormalizeExisting(seedPath, PlantCounts[index]);

            if (AssetDatabase.LoadAssetAtPath<GameObject>(growingPath) == null)
                CreateStage(seedPath, growingPath, Model(name, "Growing"));
            NormalizeExisting(growingPath, PlantCounts[index]);

            if (AssetDatabase.LoadAssetAtPath<GameObject>(grownPath) == null)
                CreateStage(growingPath, grownPath, Model(name, "Grown"));
            NormalizeExisting(grownPath, PlantCounts[index]);

            crop.plantedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(seedPath);
            crop.growingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(growingPath);
            crop.grownPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(grownPath);
            crop.harvestAmount = HarvestCounts[index];
            crop.seedPrice = SeedPrices[index];
            crop.sellPrice = SalePrices[index];
            crop.plantedSeconds = PlantedTimes[index];
            crop.growingSeconds = GrowingTimes[index];
            crop.initialHydration = InitialHydration[index];
            crop.hydrationLossPerSecond = HydrationLoss[index];
            crop.waterCost = WaterCosts[index];
            crop.hydrationPerWatering = WaterRecovery[index];
            if (crop.deadPrefab == null) throw new Exception("Missing provisional dead model: " + name);
            EditorUtility.SetDirty(crop);
            report.AppendLine(name + ": " + PlantCounts[index] + " visual plants; harvest=" + crop.harvestAmount +
                "; seed=" + crop.seedPrice + "; sale=" + crop.sellPrice + "; duration=" +
                (crop.plantedSeconds + crop.growingSeconds) + "; dead=" + AssetDatabase.GetAssetPath(crop.deadPrefab));
        }

        foreach (var root in scene.GetRootGameObjects())
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null || behaviour.GetType().Assembly.GetName().Name != "Assembly-CSharp") continue;
                var data = new SerializedObject(behaviour);
                var property = data.GetIterator();
                bool changed = false;
                while (property.NextVisible(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    if (property.objectReferenceValue == broccoli) { property.objectReferenceValue = lettuce; changed = true; }
                    if (property.objectReferenceValue == cauliflower) { property.objectReferenceValue = wheat; changed = true; }
                }
                if (changed) data.ApplyModifiedPropertiesWithoutUndo();
            }

        int changedMissions = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:MissionData", new[] { Root + "Missions" }))
        {
            var mission = AssetDatabase.LoadAssetAtPath<MissionData>(AssetDatabase.GUIDToAssetPath(guid));
            if (mission.requiredCrop == broccoli) { mission.requiredCrop = lettuce; changedMissions++; EditorUtility.SetDirty(mission); }
            else if (mission.requiredCrop == cauliflower) { mission.requiredCrop = wheat; changedMissions++; EditorUtility.SetDirty(mission); }
        }

        var hud = UnityEngine.Object.FindFirstObjectByType<FarmHUD>();
        var menu = UnityEngine.Object.FindFirstObjectByType<FarmPlotContextMenu>();
        RefreshEditorDropdown(hud, "availableCrops", "shopCropDropdown");
        RefreshEditorDropdown(menu, "availableCrops", "cropDropdown");
        AdjustCameraFraming(camera, scene.GetRootGameObjects());

        foreach (var entry in originalTransforms)
            if (entry.Key != camera.transform && EditorJsonUtility.ToJson(entry.Key) != entry.Value)
                throw new Exception("Existing scene transform changed: " + entry.Key.name);
        if (camera.transform.localEulerAngles != cameraRotation || camera.transform.localScale != cameraScale)
            throw new Exception("Camera angle or scale changed.");
        report.AppendLine("Changed mission crop references: " + changedMissions);
        report.AppendLine("Legacy Broccoli and Cauliflower assets kept; inactive in MainLevel catalogs.");
        report.AppendLine("All scene transforms preserved except the approved camera framing translation; camera angle and scale preserved.");
        report.AppendLine("Original materials and model imports preserved; Lettuce groups uniformly reduced by 20% as approved.");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("../../Verification/CropGroups");
        File.WriteAllText("../../Verification/CropGroups/Integration.txt", report.ToString());
        Debug.Log("Crop groups created and connected.");
    }

    private static void CreateStage(string templatePath, string outputPath, GameObject model)
    {
        var template = PrefabUtility.LoadPrefabContents(templatePath);
        var root = new GameObject(Path.GetFileNameWithoutExtension(outputPath));
        root.transform.localScale = template.transform.localScale;
        root.transform.localRotation = template.transform.localRotation;
        try
        {
            int index = 0;
            foreach (Transform oldPlant in template.transform)
            {
                var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(oldPlant.gameObject);
                if (source == null) throw new Exception("Group plant is not a model instance: " + oldPlant.name);
                var plant = (GameObject)PrefabUtility.InstantiatePrefab(model);
                plant.name = model.name + "_" + (++index).ToString("00");
                plant.transform.SetParent(root.transform, false);
                plant.transform.localPosition = oldPlant.localPosition;
                plant.transform.localRotation = oldPlant.localRotation;
                // Mantém o ajuste do usuário sem perder a escala nativa de cada FBX.
                plant.transform.localScale = Vector3.Scale(model.transform.localScale,
                    Divide(oldPlant.localScale, source.transform.localScale));
                Vector3 desired = template.transform.InverseTransformPoint(BoundsOf(oldPlant.gameObject).center);
                Vector3 actual = root.transform.InverseTransformPoint(BoundsOf(plant).center);
                plant.transform.localPosition += new Vector3(desired.x - actual.x, 0f, desired.z - actual.z);
            }
            NormalizeRoot(root);
            RecordNestedTransforms(root);
            PrefabUtility.SaveAsPrefabAsset(root, outputPath);
            report.AppendLine("Created " + outputPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(template);
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void CreateWheatSeed(string path)
    {
        var model = Model("Wheat", "Seed");
        var mature = (GameObject)PrefabUtility.InstantiatePrefab(Model("Wheat", "Grown"));
        var matureBounds = BoundsOf(mature);
        float fit = Mathf.Min(1f, 0.55f / matureBounds.size.x, 1.05f / matureBounds.size.z);
        UnityEngine.Object.DestroyImmediate(mature);
        var root = new GameObject(Path.GetFileNameWithoutExtension(path));
        try
        {
            for (int row = 0; row < 2; row++)
                for (int column = 0; column < 5; column++)
                {
                    var plant = (GameObject)PrefabUtility.InstantiatePrefab(model);
                    plant.name = model.name + "_" + (row * 5 + column + 1).ToString("00");
                    plant.transform.SetParent(root.transform, false);
                    plant.transform.localScale = model.transform.localScale * fit;
                    plant.transform.localPosition = new Vector3((column - 2) * 0.65f, 0f, (row - 0.5f) * 1.3f);
                    Vector3 target = plant.transform.localPosition;
                    Vector3 center = root.transform.InverseTransformPoint(BoundsOf(plant).center);
                    plant.transform.localPosition += new Vector3(target.x - center.x, 0f, target.z - center.z);
                }
            NormalizeRoot(root);
            RecordNestedTransforms(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            report.AppendLine("Created wheat Seed: two rows of five; uniform import multiplier=" + fit);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void NormalizeExisting(string path, int expectedCount)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (root.transform.childCount != expectedCount)
                throw new Exception("Unexpected group count: " + path);
            if (path.Contains("Lettuce")) root.transform.localScale = Vector3.one * 0.8f;
            NormalizeRoot(root);
            RecordNestedTransforms(root);
            var bounds = BoundsOf(root);
            report.AppendLine(path + " | count=" + expectedCount + " | size=" + bounds.size.ToString("F3") +
                " | bottom=" + bounds.min.y.ToString("F3"));
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void NormalizeRoot(GameObject root)
    {
        root.transform.localPosition = Vector3.zero;
        Vector3 center = Vector3.zero;
        foreach (Transform plant in root.transform) center += BoundsOf(plant.gameObject).center;
        center /= root.transform.childCount;
        Vector3 horizontal = root.transform.InverseTransformVector(new Vector3(-center.x, 0f, -center.z));
        foreach (Transform plant in root.transform)
        {
            plant.localPosition += horizontal;
            float height = surfaceHeight - BoundsOf(plant.gameObject).min.y;
            plant.localPosition += root.transform.InverseTransformVector(Vector3.up * height);
        }
    }

    private static void RecordNestedTransforms(GameObject root)
    {
        foreach (Transform plant in root.transform)
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(plant.gameObject);
            PrefabUtility.RecordPrefabInstancePropertyModifications(plant);
        }
    }

    private static void AdjustCameraFraming(Camera camera, GameObject[] sceneRoots)
    {
        var bounds = new List<Bounds>();
        foreach (var sceneRoot in sceneRoots)
            foreach (var plot in sceneRoot.GetComponentsInChildren<FarmPlot>(true))
            {
                var anchor = new SerializedObject(plot).FindProperty("cropVisualAnchor").objectReferenceValue as Transform;
                if (anchor == null) anchor = plot.transform;
                foreach (string name in Names)
                {
                    var crop = Load<CropData>("Crops/" + name + ".asset");
                    var instance = UnityEngine.Object.Instantiate(crop.grownPrefab);
                    instance.transform.position = anchor.TransformPoint(crop.grownPrefab.transform.localPosition);
                    instance.transform.rotation = anchor.rotation * crop.grownPrefab.transform.localRotation;
                    instance.transform.localScale = Vector3.Scale(anchor.lossyScale, crop.grownPrefab.transform.localScale);
                    bounds.Add(BoundsOf(instance));
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }

        Vector3 previousPosition = camera.transform.position;
        var previousTarget = camera.targetTexture;
        var target = new RenderTexture(1600, 900, 16);
        try
        {
            camera.targetTexture = target;
            int steps = 0;
            while (ViewportRange(camera, bounds).y > 0.95f && steps++ < 60)
                camera.transform.position += camera.transform.up * 0.05f;
            Vector2 range = ViewportRange(camera, bounds);
            if (range.y > 0.95f || range.x < 0.1f)
                throw new Exception("Camera requires a larger framing change than the approved small translation.");
            report.AppendLine("Camera framing: " + previousPosition.ToString("F3") + " -> " +
                camera.transform.position.ToString("F3") + "; crop viewport Y=" + range.ToString("F3"));
        }
        finally
        {
            camera.targetTexture = previousTarget;
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    private static Vector2 ViewportRange(Camera camera, List<Bounds> bounds)
    {
        float bottom = float.PositiveInfinity;
        float top = float.NegativeInfinity;
        foreach (var box in bounds)
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 point = box.center + Vector3.Scale(box.extents, new Vector3(x, y, z));
                        Vector3 view = camera.WorldToViewportPoint(point);
                        bottom = Mathf.Min(bottom, view.y);
                        top = Mathf.Max(top, view.y);
                    }
        return new Vector2(bottom, top);
    }

    private static Bounds BoundsOf(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) throw new Exception("No crop renderer: " + root.name);
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    private static Vector3 Divide(Vector3 value, Vector3 divisor) =>
        new Vector3(value.x / divisor.x, value.y / divisor.y, value.z / divisor.z);

    private static GameObject Model(string crop, string stage) =>
        Load<GameObject>("Models/Crops/" + stage + "/SM_Crop_" + crop + "_Stage_" + stage + ".fbx");

    private static T Load<T>(string path) where T : UnityEngine.Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(Root + path);
        if (asset == null) throw new Exception("Asset not found: " + path);
        return asset;
    }

    private static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
    }

    private static void RefreshEditorDropdown(MonoBehaviour component, string cropField, string dropdownField)
    {
        var data = new SerializedObject(component);
        var crops = data.FindProperty(cropField);
        var dropdown = data.FindProperty(dropdownField).objectReferenceValue as TMP_Dropdown;
        if (dropdown == null) return;
        var options = new List<TMP_Dropdown.OptionData>();
        for (int index = 0; index < crops.arraySize; index++)
        {
            var crop = crops.GetArrayElementAtIndex(index).objectReferenceValue as CropData;
            options.Add(new TMP_Dropdown.OptionData(crop != null ? crop.CropName : "Crop not configured",
                crop != null ? crop.icon : null, Color.white));
        }
        dropdown.ClearOptions();
        dropdown.AddOptions(options);
        dropdown.SetValueWithoutNotify(0);
        EditorUtility.SetDirty(dropdown);
    }
}
