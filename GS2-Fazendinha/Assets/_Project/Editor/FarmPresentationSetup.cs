using System.IO;

using System.Text;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEditor.Animations;
using System.Linq;

public static class FarmPresentationSetup
{
    public static void FinishScales()
    {
        var scene = EditorSceneManager.OpenScene(Root + "Scenes/MainLevel.unity");
        var camera = Camera.main;
        var frames = new System.Collections.Generic.List<Bounds>();
        frames.Add(FarmCropPrefabRefresh.BoundsOf(GameObject.Find("FarmBarn")));
        foreach (var plot in Object.FindObjectsByType<FarmPlot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            frames.Add(FarmCropPrefabRefresh.BoundsOf(plot.transform.Find("SoilMesh").gameObject));
        foreach (var actor in Object.FindObjectsByType<PlayerFarmController>(FindObjectsSortMode.None))
            frames.Add(FarmCropModelInspection.SkinBounds(actor.GetComponentInChildren<Animator>().gameObject));
        float left = float.PositiveInfinity, right = float.NegativeInfinity;
        float bottom = float.PositiveInfinity, top = float.NegativeInfinity;
        foreach (var bounds in frames)
        for (int x = -1; x <= 1; x += 2)
        for (int y = -1; y <= 1; y += 2)
        for (int z = -1; z <= 1; z += 2)
        {
            var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
            float horizontal = Vector3.Dot(corner, camera.transform.right);
            float vertical = Vector3.Dot(corner, camera.transform.up);
            left = Mathf.Min(left, horizontal); right = Mathf.Max(right, horizontal);
            bottom = Mathf.Min(bottom, vertical); top = Mathf.Max(top, vertical);
        }
        float aspect = 16f / 9f;
        camera.orthographicSize = Mathf.Max(8.21f, (top - bottom) / 1.7f, (right - left) / (1.86f * aspect));
        float targetX = (left + right) * 0.5f + 0.06f * camera.orthographicSize * aspect;
        float targetY = (bottom + top) * 0.5f - 0.05f * camera.orthographicSize;
        camera.transform.position += camera.transform.right * (targetX - Vector3.Dot(camera.transform.position, camera.transform.right)) +
            camera.transform.up * (targetY - Vector3.Dot(camera.transform.position, camera.transform.up));
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        VerifyScales();
        EditorSceneManager.OpenScene(Root + "Scenes/MainMenu.unity");
        foreach (var menu in Object.FindObjectsByType<MenuFarmBackdrop>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var entries = new SerializedObject(menu).FindProperty("plots");
            for (int index = 0; index < entries.arraySize; index++)
            {
                var entry = entries.GetArrayElementAtIndex(index);
                var anchor = (Transform)entry.FindPropertyRelative("cropAnchor").objectReferenceValue;
                float soil = FarmCropPrefabRefresh.BoundsOf(anchor.parent.Find("PlotVisual").gameObject).max.y;
                foreach (string field in new[] { "plantedModel", "growingModel", "grownModel" })
                {
                    var model = (GameObject)entry.FindPropertyRelative(field).objectReferenceValue;
                    var bounds = FarmCropPrefabRefresh.BoundsOf(model);
                    if (Mathf.Abs(bounds.min.y - soil) > 0.02f || (index == 0 && bounds.size.y > 0.77f)) throw new System.Exception("Menu crop scale/grounding mismatch.");
                }
            }
        }
        File.AppendAllText("../../Verification/PlayerAnimation/ScalesVerified.txt", "PASS all decorative menu stages match gameplay scales and soil height\n");
    }

    public static void ApplyScales()
    {
        var report = new StringBuilder();
        var changed = new System.Collections.Generic.HashSet<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:CropData", new[] { Root }))
        {
            var crop = AssetDatabase.LoadAssetAtPath<CropData>(AssetDatabase.GUIDToAssetPath(guid));
            if (crop.grownPrefab == null) continue;
            float height = FarmCropPrefabRefresh.BoundsOf(crop.grownPrefab).size.y;
            bool carrot = crop.name.Contains("Carrot");
            float factor = carrot ? 0.75f / height : 0.9f;
            foreach (var source in new[] { crop.plantedPrefab, crop.growingPrefab, crop.grownPrefab, crop.deadPrefab })
            {
                if (source == null) continue;
                string path = AssetDatabase.GetAssetPath(source);
                if (!changed.Add(path)) continue;
                var prefab = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    float resize = factor;
                    if (carrot && source == crop.deadPrefab) resize = Mathf.Min(resize, 0.75f / FarmCropPrefabRefresh.BoundsOf(prefab).size.y);
                    if (prefab.transform.childCount == 0)
                    {
                        prefab.transform.localScale *= resize;
                        prefab.transform.position += Vector3.down * FarmCropPrefabRefresh.BoundsOf(prefab).min.y;
                    }
                    else
                    {
                        prefab.transform.position = Vector3.zero;
                        foreach (Transform model in prefab.transform)
                        {
                            if (model.GetComponentsInChildren<Renderer>(true).Length == 0) continue;
                            model.localScale *= resize;
                            model.position += Vector3.down * FarmCropPrefabRefresh.BoundsOf(model.gameObject).min.y;
                            if (PrefabUtility.IsPartOfPrefabInstance(model)) PrefabUtility.RecordPrefabInstancePropertyModifications(model);
                        }
                    }
                    PrefabUtility.SaveAsPrefabAsset(prefab, path);
                    report.AppendLine("CROP " + path + " height=" + FarmCropPrefabRefresh.BoundsOf(prefab).size.y);
                }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
            }
        }
        string barnPath = Root + "Prefabs/Buildings/FarmBarn.prefab";
        var barnPrefab = PrefabUtility.LoadPrefabContents(barnPath);
        try { ResizeBarn(barnPrefab.transform); PrefabUtility.SaveAsPrefabAsset(barnPrefab, barnPath); }
        finally { PrefabUtility.UnloadPrefabContents(barnPrefab); }
        string menuPath = Root + "Prefabs/Menu/MenuFarmBackdrop.prefab";
        var menuPrefab = PrefabUtility.LoadPrefabContents(menuPath);
        try
        {
            ResizeSceneObjects(menuPrefab, report);
            RefreshMenuCrops(menuPrefab.GetComponent<MenuFarmBackdrop>());
            PrefabUtility.SaveAsPrefabAsset(menuPrefab, menuPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(menuPrefab); }
        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { Root + "Scenes" }))
        {
            var scene = EditorSceneManager.OpenScene(AssetDatabase.GUIDToAssetPath(guid));
            report.AppendLine("SCENE " + scene.name);
            foreach (var menu in Object.FindObjectsByType<MenuFarmBackdrop>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var data = new SerializedObject(menu);
                var animator = (Animator)data.FindProperty("farmerAnimator").objectReferenceValue;
                PlayerClip("Idle1").SampleAnimation(animator.gameObject, 0f);
                if (Mathf.Abs(FarmCropModelInspection.SkinBounds(animator.gameObject).size.y - 2.5f) > 0.03f) throw new System.Exception("Menu character scale mismatch.");
                var plots = data.FindProperty("plots");
                for (int index = 0; index < plots.arraySize; index++)
                {
                    var entry = plots.GetArrayElementAtIndex(index);
                    var anchor = (Transform)entry.FindPropertyRelative("cropAnchor").objectReferenceValue;
                    float soil = FarmCropPrefabRefresh.BoundsOf(anchor.parent.Find("PlotVisual").gameObject).max.y;
                    foreach (string field in new[] { "plantedModel", "growingModel", "grownModel" })
                    {
                        var model = (GameObject)entry.FindPropertyRelative(field).objectReferenceValue;
                        var size = FarmCropPrefabRefresh.BoundsOf(model);
                        if (Mathf.Abs(size.min.y - soil) > 0.02f) throw new System.Exception("Menu crop grounding mismatch.");
                        if (index == 0 && size.size.y > 0.77f) throw new System.Exception("Menu carrot scale mismatch.");
                    }
                }
                report.AppendLine("PASS decorative character and all menu crop stages match gameplay scales");
            }
            foreach (var root in scene.GetRootGameObjects()) ResizeSceneObjects(root, report);
            foreach (var plot in Object.FindObjectsByType<FarmPlot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var anchor = (Transform)new SerializedObject(plot).FindProperty("cropVisualAnchor").objectReferenceValue;
                anchor.position = new Vector3(anchor.position.x, FarmCropPrefabRefresh.BoundsOf(plot.transform.Find("SoilMesh").gameObject).max.y, anchor.position.z);
                if (PrefabUtility.IsPartOfPrefabInstance(anchor)) PrefabUtility.RecordPrefabInstancePropertyModifications(anchor);
            }
            foreach (var menu in Object.FindObjectsByType<MenuFarmBackdrop>(FindObjectsInactive.Include, FindObjectsSortMode.None)) RefreshMenuCrops(menu);
            if (scene.name == "MainLevel")
            {
                foreach (var surface in Object.FindObjectsByType<Unity.AI.Navigation.NavMeshSurface>(FindObjectsSortMode.None))
                {
                    var previous = surface.navMeshData;
                    surface.BuildNavMesh();
                    if (previous != null && AssetDatabase.Contains(previous))
                    {
                        var generated = surface.navMeshData;
                        surface.RemoveData();
                        EditorUtility.CopySerialized(generated, previous);
                        surface.navMeshData = previous;
                        surface.AddData();
                        EditorUtility.SetDirty(previous);
                    }
                }
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        File.WriteAllText("../../Verification/PlayerAnimation/ScalesApplied.txt", report.ToString());
        VerifyScales();
    }

    private static void ResizeSceneObjects(GameObject root, StringBuilder report)
    {
        foreach (var animator in root.GetComponentsInChildren<Animator>(true))
        {
            var visual = animator.transform.parent;
            if (visual == null || visual.name != "PlayerVisual") continue;
            PlayerClip("Idle1").SampleAnimation(animator.gameObject, 0f);
            var bounds = FarmCropModelInspection.SkinBounds(animator.gameObject);
            float bottom = bounds.min.y;
            visual.localScale *= 2.5f / bounds.size.y;
            visual.position += Vector3.up * (bottom - FarmCropModelInspection.SkinBounds(animator.gameObject).min.y);
            var actor = visual.GetComponentInParent<PlayerFarmController>();
            if (actor != null)
            {
                actor.GetComponent<UnityEngine.AI.NavMeshAgent>().height = 2.5f;
                var collider = actor.GetComponent<CapsuleCollider>();
                if (collider != null)
                {
                    float delta = 2.5f - collider.height;
                    collider.center += Vector3.up * delta * 0.5f;
                    collider.height = 2.5f;
                }
            }
            if (PrefabUtility.IsPartOfPrefabInstance(visual)) PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
            report.AppendLine("PLAYER height=" + FarmCropModelInspection.SkinBounds(animator.gameObject).size.y);
        }
        foreach (var node in root.GetComponentsInChildren<Transform>(true))
        {
            if (node.name != "FarmBarn" && node.name != "SM_Farm_Building_Barn") continue;
            bool nested = false;
            for (var parent = node.parent; parent != null; parent = parent.parent) if (parent.name == "FarmBarn") nested = true;
            if (!nested) { ResizeBarn(node); report.AppendLine("BARN height=" + FarmCropPrefabRefresh.BoundsOf(node.gameObject).size.y); }
        }
    }

    private static void ResizeBarn(Transform barn)
    {
        var bounds = FarmCropPrefabRefresh.BoundsOf(barn.gameObject);
        float bottom = bounds.min.y;
        barn.localScale *= 8.46f / bounds.size.y;
        barn.position += Vector3.up * (bottom - FarmCropPrefabRefresh.BoundsOf(barn.gameObject).min.y);
        if (PrefabUtility.IsPartOfPrefabInstance(barn)) PrefabUtility.RecordPrefabInstancePropertyModifications(barn);
    }

    private static void RefreshMenuCrops(MenuFarmBackdrop menu)
    {
        var data = new SerializedObject(menu);
        var plots = data.FindProperty("plots");
        string[] crops = { "Carrot", "Corn", "Lettuce", "Wheat" };
        for (int index = 0; index < plots.arraySize; index++)
        {
            var entry = plots.GetArrayElementAtIndex(index);
            var anchor = (Transform)entry.FindPropertyRelative("cropAnchor").objectReferenceValue;
            var soil = anchor.parent.Find("PlotVisual");
            anchor.position = new Vector3(anchor.position.x, FarmCropPrefabRefresh.BoundsOf(soil.gameObject).max.y, anchor.position.z);
            var crop = AssetDatabase.LoadAssetAtPath<CropData>(Root + "Crops/" + crops[index % crops.Length] + ".asset");
            string[] fields = { "plantedModel", "growingModel", "grownModel" };
            var sources = new[] { crop.plantedPrefab, crop.growingPrefab, crop.grownPrefab };
            for (int stage = 0; stage < fields.Length; stage++)
            {
                var field = entry.FindPropertyRelative(fields[stage]);
                var old = (GameObject)field.objectReferenceValue;
                string name = old != null ? old.name : fields[stage];
                if (old != null) Object.DestroyImmediate(old);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(sources[stage], anchor);
                model.name = name;
                model.SetActive(false);
                field.objectReferenceValue = model;
            }
        }
        data.ApplyModifiedPropertiesWithoutUndo();
        if (PrefabUtility.IsPartOfPrefabInstance(menu)) PrefabUtility.RecordPrefabInstancePropertyModifications(menu);
    }

    public static void VerifyScales()
    {
        var report = new StringBuilder();
        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { Root + "Scenes" }))
        {
            var scene = EditorSceneManager.OpenScene(AssetDatabase.GUIDToAssetPath(guid));
            report.AppendLine("SCENE " + scene.name);
            foreach (var actor in Object.FindObjectsByType<PlayerFarmController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var animator = actor.GetComponentInChildren<Animator>(true);
                PlayerClip("Idle1").SampleAnimation(animator.gameObject, 0f);
                float height = FarmCropModelInspection.SkinBounds(animator.gameObject).size.y;
                if (Mathf.Abs(height - 2.5f) > 0.03f) throw new System.Exception("Player scale mismatch: " + scene.name);
                report.AppendLine("PASS player height=" + height);
            }
            foreach (var plot in Object.FindObjectsByType<FarmPlot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var anchor = (Transform)new SerializedObject(plot).FindProperty("cropVisualAnchor").objectReferenceValue;
                float ground = FarmCropPrefabRefresh.BoundsOf(plot.transform.Find("SoilMesh").gameObject).max.y;
                foreach (string name in new[] { "Carrot", "Corn", "Lettuce", "Sunflower", "Wheat" })
                {
                    var crop = AssetDatabase.LoadAssetAtPath<CropData>(Root + "Crops/" + name + ".asset");
                    foreach (var prefab in new[] { crop.plantedPrefab, crop.growingPrefab, crop.grownPrefab, crop.deadPrefab })
                    {
                        var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, anchor);
                        var bounds = FarmCropPrefabRefresh.BoundsOf(model);
                        if (Mathf.Abs(bounds.min.y - ground) > 0.02f) throw new System.Exception("Crop is above/below soil: " + scene.name + " " + prefab.name);
                        if (name == "Carrot" && bounds.size.y > 0.77f) throw new System.Exception("Carrot is too large.");
                        Object.DestroyImmediate(model);
                    }
                }
                report.AppendLine("PASS all crop stages grounded: " + plot.name);
            }
            if (scene.name == "MainLevel" || scene.name == "Tutorial")
            {
                var plot = Object.FindFirstObjectByType<FarmPlot>();
                var anchor = (Transform)new SerializedObject(plot).FindProperty("cropVisualAnchor").objectReferenceValue;
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<CropData>(Root + "Crops/Carrot.asset").grownPrefab, anchor);
                SessionState.SetString("FarmSystemAuditReport", "../../Verification/PlayerAnimation");
                FarmSystemChecks.Capture("Scales" + scene.name);
                Object.DestroyImmediate(model);
            }
        }
        File.WriteAllText("../../Verification/PlayerAnimation/ScalesVerified.txt", report.ToString());
    }

    public static void InspectScales()
    {
        var report = new StringBuilder();
        foreach (string guid in AssetDatabase.FindAssets("t:CropData", new[] { Root }))
        {
            var crop = AssetDatabase.LoadAssetAtPath<CropData>(AssetDatabase.GUIDToAssetPath(guid));
            report.AppendLine("CROP " + crop.name);
            foreach (var source in new[] { crop.plantedPrefab, crop.growingPrefab, crop.grownPrefab, crop.deadPrefab })
            {
                if (source == null) continue;
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
                report.AppendLine(source.name + " scale=" + instance.transform.localScale + " bounds=" + FarmCropPrefabRefresh.BoundsOf(instance));
                Object.DestroyImmediate(instance);
            }
        }
        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { Root + "Scenes" }))
        {
            var scene = EditorSceneManager.OpenScene(AssetDatabase.GUIDToAssetPath(guid));
            report.AppendLine("SCENE " + scene.name);
            foreach (var root in scene.GetRootGameObjects())
            foreach (var node in root.GetComponentsInChildren<Transform>(true))
            {
                if (node.name.Contains("Barn") && node.GetComponentsInChildren<Renderer>(true).Length > 0) report.AppendLine("BARN " + node.name + " scale=" + node.lossyScale + " bounds=" + FarmCropPrefabRefresh.BoundsOf(node.gameObject));
            }
            foreach (var plot in Object.FindObjectsByType<FarmPlot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var anchor = (Transform)new SerializedObject(plot).FindProperty("cropVisualAnchor").objectReferenceValue;
                report.AppendLine("PLOT " + plot.name + " scale=" + plot.transform.lossyScale + " anchor=" + anchor.lossyScale + " soil=" + FarmCropPrefabRefresh.BoundsOf(plot.transform.Find("SoilMesh").gameObject));
            }
            foreach (var actor in Object.FindObjectsByType<PlayerFarmController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                report.AppendLine("PLAYER " + actor.name + " height=" + actor.GetComponent<UnityEngine.AI.NavMeshAgent>().height * actor.transform.lossyScale.y);
        }
        File.WriteAllText("../../Verification/PlayerAnimation/ScalesBefore.txt", report.ToString());
    }

    public static void FixPlayerFacing()
    {
        string path = Root + "Prefabs/Menu/MenuFarmBackdrop.prefab";
        var prefab = PrefabUtility.LoadPrefabContents(path);
        try
        {
            CorrectFacing(prefab);
            PrefabUtility.SaveAsPrefabAsset(prefab, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { Root + "Scenes" }))
        {
            var scene = EditorSceneManager.OpenScene(AssetDatabase.GUIDToAssetPath(guid));
            foreach (var root in scene.GetRootGameObjects()) CorrectFacing(root);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        FarmCropModelInspection.RunPlayerCheck();
    }

    private static void CorrectFacing(GameObject root)
    {
        foreach (var animator in root.GetComponentsInChildren<Animator>(true))
        {
            var visual = animator.transform.parent;
            if (visual == null || visual.name != "PlayerVisual") continue;
            visual.localRotation = Quaternion.Euler(0f, 180f, 0f);
            if (PrefabUtility.IsPartOfPrefabInstance(visual)) PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
        }
    }

    public static void ApplyPipeIcon()
    {
        const string path = Root + "UI/Sprites/Tech/Canos(3).png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) throw new System.Exception("Pipe icon was not imported.");
        var first = AssetDatabase.LoadAssetAtPath<TechnologyData>(Root + "Tech/Le Fezend/Reinforced Pipes I.asset");
        var second = AssetDatabase.LoadAssetAtPath<TechnologyData>(Root + "Tech/Le Fezend/Reinforced Pipes II.asset");
        second.icon = sprite;
        EditorUtility.SetDirty(second);
        AssetDatabase.SaveAssets();
        if (first.icon == null || first.icon == second.icon) throw new System.Exception("Pipe upgrades must have different icons.");
        Debug.Log("Pipe icons verified: I=" + first.icon.name + ", II=" + second.icon.name);
    }

    public static void ApplyWaterIcon()
    {
        ApplyActionIcon("Regador", "waterButton", "WaterIcon");
    }

    public static void ApplyHarvestIcon()
    {
        ApplyActionIcon("Ancinho", "harvestButton", "HarvestIcon");
    }

    private static void ApplyActionIcon(string asset, string field, string iconName)
    {
        string path = Root + "UI/Sprites/Tech/" + asset + ".png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) throw new System.Exception(asset + " was not imported.");
        foreach (string name in new[] { "MainLevel", "Tutorial", "Testes" })
        {
            var scene = EditorSceneManager.OpenScene(Root + "Scenes/" + name + ".unity");
            foreach (var menu in Object.FindObjectsByType<FarmPlotContextMenu>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var data = new SerializedObject(menu);
                var button = (UnityEngine.UI.Button)data.FindProperty(field).objectReferenceValue;
                var text = button != null ? button.GetComponentInChildren<TMPro.TMP_Text>(true) : null;
                if (button == null || text == null) continue;
                var icon = button.transform.Find(iconName) as RectTransform;
                if (icon == null)
                {
                    icon = new GameObject(iconName, typeof(RectTransform), typeof(UnityEngine.UI.Image)).GetComponent<RectTransform>();
                    icon.SetParent(button.transform, false);
                }
                icon.gameObject.layer = button.gameObject.layer;
                icon.anchorMin = icon.anchorMax = new Vector2(0f, 0.5f);
                icon.pivot = new Vector2(0f, 0.5f);
                icon.anchoredPosition = new Vector2(12f, 0f);
                icon.sizeDelta = new Vector2(32f, 32f);
                var image = icon.GetComponent<UnityEngine.UI.Image>();
                image.sprite = sprite;
                image.preserveAspect = true;
                image.raycastTarget = false;
                text.rectTransform.anchorMin = Vector2.zero;
                text.rectTransform.anchorMax = Vector2.one;
                text.rectTransform.offsetMin = new Vector2(50f, 5f);
                text.rectTransform.offsetMax = new Vector2(-8f, -5f);
                text.raycastTarget = false;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(iconName + " connected: " + name);
            if (name == "MainLevel")
            {
                var menu = Object.FindFirstObjectByType<FarmPlotContextMenu>(FindObjectsInactive.Include);
                var data = new SerializedObject(menu);
                var panel = (RectTransform)data.FindProperty("menuPanel").objectReferenceValue;
                panel.gameObject.SetActive(true);
                panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
                panel.anchoredPosition = Vector2.zero;
                foreach (var button in panel.GetComponentsInChildren<UnityEngine.UI.Button>(true)) button.gameObject.SetActive(true);
                SessionState.SetString("FarmSystemAuditReport", "../../Verification/PlayerAnimation");
                FarmSystemChecks.Capture("ActionIcons");
            }
        }
        AssetDatabase.SaveAssets();
    }

    public static void ApplyClockIcon()
    {
        const string path = "Assets/_Project/Models/Relogio.png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        var technology = AssetDatabase.LoadAssetAtPath<TechnologyData>("Assets/_Project/Tech/FarmUpgrades/Show Growing Time.asset");
        technology.icon = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        EditorUtility.SetDirty(technology);
        AssetDatabase.SaveAssets();
        if (technology.icon == null) throw new System.Exception("Growing Timer icon was not imported.");
    }

    private const string Root = "Assets/_Project/";
    private const string Reports = "../../Verification/Presentation/";
    public const string PlayerModel = Root + "Models/Player/Character_Models_Animations.fbx";
    public const string PlayerController = Root + "Animations/Player.controller";

    public static void ApplyPlayer()
    {
        Directory.CreateDirectory(Root + "Animations");
        Directory.CreateDirectory(Root + "Materials/Player");
        AssetDatabase.Refresh();
        var importer = (ModelImporter)AssetImporter.GetAtPath(PlayerModel);
        var clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
        bool reimport = importer.clipAnimations.Length == 0;
        foreach (var clip in clips)
        {
            bool loop = clip.name.Contains("Idle") || clip.name.EndsWith("|Walk");
            reimport |= clip.loopTime != loop;
            clip.loopTime = loop;
        }
        if (reimport) { importer.clipAnimations = clips; importer.SaveAndReimport(); }
        var controller = BuildPlayerController();
        var material = PlayerMaterial();
        InspectLoops();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { Root + "Prefabs" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = PrefabUtility.LoadPrefabContents(path);
            try { if (RestoreMaterials(prefab)) PrefabUtility.SaveAsPrefabAsset(prefab, path); }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }
        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { Root + "Scenes" }))
        {
            var scene = EditorSceneManager.OpenScene(AssetDatabase.GUIDToAssetPath(guid));
            bool changed = false;
            foreach (var root in scene.GetRootGameObjects()) changed |= RestoreMaterials(root);
            foreach (var player in Object.FindObjectsByType<PlayerFarmController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var agent = player.GetComponent<UnityEngine.AI.NavMeshAgent>();
                var old = player.GetComponent<MeshRenderer>();
                float height = old != null ? old.bounds.size.y : agent.height * player.transform.lossyScale.y;
                if (old != null) old.enabled = false;
                var animator = player.GetComponentInChildren<Animator>(true);
                if (animator == null)
                    animator = AddPlayerModel(player.transform, height, -agent.baseOffset, controller, material);
                ConfigureAnimator(animator, controller, material, false);
                var data = new SerializedObject(player);
                data.FindProperty("animator").objectReferenceValue = animator;
                data.ApplyModifiedPropertiesWithoutUndo();
                if (PrefabUtility.IsPartOfPrefabInstance(player)) PrefabUtility.RecordPrefabInstancePropertyModifications(player);
                changed = true;
            }
            foreach (var tank in Object.FindObjectsByType<FarmWaterTank>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var data = new SerializedObject(tank);
                data.FindProperty("regenerationInterval").floatValue = 3f;
                data.ApplyModifiedPropertiesWithoutUndo();
                if (PrefabUtility.IsPartOfPrefabInstance(tank)) PrefabUtility.RecordPrefabInstancePropertyModifications(tank);
                changed = true;
            }
            if (scene.name == "Tutorial") AlignTutorialCrops();
            if (!changed) continue;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        string menuPath = Root + "Prefabs/Menu/MenuFarmBackdrop.prefab";
        var menu = PrefabUtility.LoadPrefabContents(menuPath);
        try
        {
            ConfigureMenuPlayer(menu.GetComponent<MenuFarmBackdrop>(), controller, material);
            PrefabUtility.SaveAsPrefabAsset(menu, menuPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(menu); }
        var mainMenu = EditorSceneManager.OpenScene(Root + "Scenes/MainMenu.unity");
        ConfigureMenuPlayer(Object.FindFirstObjectByType<MenuFarmBackdrop>(), controller, material);
        EditorSceneManager.MarkSceneDirty(mainMenu);
        EditorSceneManager.SaveScene(mainMenu);
        AssetDatabase.SaveAssets();
        FarmCropModelInspection.RunPlayerCheck();
    }

    public static AnimationClip PlayerClip(string name) => AssetDatabase.LoadAllAssetsAtPath(PlayerModel).OfType<AnimationClip>()
        .Single(clip => !clip.name.StartsWith("__preview__") && clip.name.EndsWith("|" + name));

    private static bool RestoreMaterials(GameObject root)
    {
        bool changed = false;
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            var materials = renderer.sharedMaterials;
            bool repaired = false;
            for (int index = 0; index < materials.Length; index++)
            {
                var material = materials[index];
                var source = PrefabUtility.GetCorrespondingObjectFromSource(renderer);
                if (material != null && material.shader != null && material.shader.name != "Hidden/InternalErrorShader")
                {
                    bool barn = false;
                    for (var parent = renderer.transform; parent != null; parent = parent.parent)
                        if (parent.name == "FarmBarn") barn = true;
                    if (barn && material.mainTexture == null && source != null && source.sharedMaterials.Length > index && source.sharedMaterials[index] != null && source.sharedMaterials[index].mainTexture != null)
                    {
                        materials[index] = source.sharedMaterials[index];
                        repaired = changed = true;
                    }
                    continue;
                }
                Material original = null;
                if (source != null && source.sharedMaterials.Length > index) original = source.sharedMaterials[index];
                if (original == material || original == null || original.shader == null || original.shader.name == "Hidden/InternalErrorShader")
                {
                    var match = System.Text.RegularExpressions.Regex.Match(material != null ? Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(material)) : "", @"_([0-9a-f]{8})_(-?\d+)$");
                    if (match.Success)
                    {
                        string guid = AssetDatabase.FindAssets("", new[] { Root }).FirstOrDefault(item => item.StartsWith(match.Groups[1].Value));
                        if (guid != null)
                            foreach (var candidate in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)).OfType<Material>())
                                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(candidate, out string _, out long id) && id.ToString() == match.Groups[2].Value) original = candidate;
                    }
                }
                if (original == null || original.shader == null || original.shader.name == "Hidden/InternalErrorShader")
                    throw new System.InvalidOperationException("Original material not found for " + renderer.name + ": " + (material != null ? material.name : "NULL"));
                materials[index] = original;
                repaired = changed = true;
            }
            if (!repaired) continue;
            renderer.sharedMaterials = materials;
            if (PrefabUtility.IsPartOfPrefabInstance(renderer)) PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }
        return changed;
    }

    private static void InspectLoops()
    {
        var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerModel));
        var report = new StringBuilder();
        try
        {
            foreach (string name in new[] { "Idle1", "Idle2", "Idle3", "Walk" })
            {
                var clip = PlayerClip(name);
                clip.SampleAnimation(model, 0f);
                var nodes = model.GetComponentsInChildren<Transform>();
                var rotations = nodes.Select(node => node.localRotation).ToArray();
                var positions = nodes.Select(node => node.position).ToArray();
                float height = FarmCropModelInspection.SkinBounds(model).size.y;
                clip.SampleAnimation(model, clip.length - 0.001f);
                float angle = 0f;
                float shift = 0f;
                for (int index = 0; index < nodes.Length; index++)
                {
                    angle = Mathf.Max(angle, Quaternion.Angle(rotations[index], nodes[index].localRotation));
                    shift = Mathf.Max(shift, Vector3.Distance(positions[index], nodes[index].position));
                }
                report.AppendLine(name + ": length=" + clip.length + " maxJointAngle=" + angle + " maxPositionFraction=" + shift / height);
            }
        }
        finally { Object.DestroyImmediate(model); }
        File.WriteAllText("../../Verification/PlayerAnimation/ClipContinuity.txt", report.ToString());
    }

    private static AnimatorController BuildPlayerController()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerController);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(PlayerController);
        var machine = controller.layers[0].stateMachine;
        foreach (var state in machine.states) machine.RemoveState(state.state);
        foreach (var transition in machine.anyStateTransitions) machine.RemoveAnyStateTransition(transition);
        controller.parameters = new AnimatorControllerParameter[0];
        controller.AddParameter("Moving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Plant", AnimatorControllerParameterType.Trigger);
        var idle = new AnimatorState[3];
        for (int index = 0; index < idle.Length; index++)
        {
            idle[index] = machine.AddState("Idle" + (index + 1), new Vector3(index * 260f, 0f));
            idle[index].motion = PlayerClip("Idle" + (index + 1));
            idle[index].tag = "Idle";
        }
        var walk = machine.AddState("Walk", new Vector3(260f, 150f));
        walk.motion = PlayerClip("Walk");
        var plant = machine.AddState("Planting2", new Vector3(260f, -180f));
        plant.motion = PlayerClip("Planting2");
        machine.defaultState = idle[0];
        for (int index = 0; index < idle.Length; index++)
        {
            var cycle = Transition(idle[index], idle[(index + 1) % idle.Length], true,
                (index == 0 ? 8f : 4f) / ((AnimationClip)idle[index].motion).length);
            cycle.AddCondition(AnimatorConditionMode.IfNot, 0f, "Moving");
            Transition(idle[index], walk, false).AddCondition(AnimatorConditionMode.If, 0f, "Moving");
        }
        Transition(walk, idle[0], false).AddCondition(AnimatorConditionMode.IfNot, 0f, "Moving");
        Transition(plant, walk, false).AddCondition(AnimatorConditionMode.If, 0f, "Moving");
        Transition(plant, idle[0], true, 1f).AddCondition(AnimatorConditionMode.IfNot, 0f, "Moving");
        var planting = machine.AddAnyStateTransition(plant);
        planting.hasExitTime = false;
        planting.duration = 0.12f;
        planting.hasFixedDuration = true;
        planting.canTransitionToSelf = false;
        planting.AddCondition(AnimatorConditionMode.If, 0f, "Plant");
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static AnimatorStateTransition Transition(AnimatorState from, AnimatorState to, bool exit, float time = 0f)
    {
        var transition = from.AddTransition(to);
        transition.hasExitTime = exit;
        transition.exitTime = time;
        transition.hasFixedDuration = true;
        transition.duration = 0.12f;
        return transition;
    }

    private static Material PlayerMaterial()
    {
        string path = Root + "Materials/Player/PlayerBody.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(AssetDatabase.LoadAllAssetsAtPath(PlayerModel).OfType<Material>().First());
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Models/Crops/Textures/textura-universal.png"));
        material.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Animator AddPlayerModel(Transform parent, float height, float groundOffset, AnimatorController controller, Material material)
    {
        var visual = new GameObject("PlayerVisual").transform;
        visual.SetParent(parent, false);
        visual.position = parent.position + Vector3.up * groundOffset;
        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerModel), visual);
        PlayerClip("Idle1").SampleAnimation(model, 0f);
        var bounds = FarmCropModelInspection.SkinBounds(model);
        visual.localScale *= height / bounds.size.y;
        bounds = FarmCropModelInspection.SkinBounds(model);
        model.transform.position += new Vector3(parent.position.x - bounds.center.x, visual.position.y - bounds.min.y, parent.position.z - bounds.center.z);
        PrefabUtility.RecordPrefabInstancePropertyModifications(model.transform);
        var animator = model.GetComponent<Animator>();
        if (animator == null) animator = model.AddComponent<Animator>();
        ConfigureAnimator(animator, controller, material, false);
        return animator;
    }

    private static void ConfigureAnimator(Animator animator, AnimatorController controller, Material material, bool menu)
    {
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.updateMode = menu ? AnimatorUpdateMode.UnscaledTime : AnimatorUpdateMode.Normal;
        foreach (var renderer in animator.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            renderer.sharedMaterial = material;
            renderer.updateWhenOffscreen = true;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }
        PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
    }

    private static void ConfigureMenuPlayer(MenuFarmBackdrop menu, AnimatorController controller, Material material)
    {
        var data = new SerializedObject(menu);
        var farmer = (Transform)data.FindProperty("farmer").objectReferenceValue;
        var visual = (Transform)data.FindProperty("farmerVisual").objectReferenceValue;
        var animator = farmer.GetComponentInChildren<Animator>(true);
        if (animator == null)
        {
            var bounds = FarmCropPrefabRefresh.BoundsOf(visual.gameObject);
            Object.DestroyImmediate(visual.gameObject);
            animator = AddPlayerModel(farmer, bounds.size.y, bounds.min.y - farmer.position.y, controller, material);
        }
        ConfigureAnimator(animator, controller, material, true);
        var ground = menu.GetComponentsInChildren<Transform>(true).First(node => node.name == "Ground");
        float groundHeight = FarmCropPrefabRefresh.BoundsOf(ground.gameObject).max.y;
        float centerHeight = groundHeight + 1f;
        farmer.position = new Vector3(farmer.position.x, centerHeight, farmer.position.z);
        var plots = data.FindProperty("plots");
        for (int index = 0; index < plots.arraySize; index++)
        {
            var point = (Transform)plots.GetArrayElementAtIndex(index).FindPropertyRelative("workPoint").objectReferenceValue;
            point.position = new Vector3(point.position.x, centerHeight, point.position.z);
            if (PrefabUtility.IsPartOfPrefabInstance(point)) PrefabUtility.RecordPrefabInstancePropertyModifications(point);
        }
        animator.transform.parent.localPosition = Vector3.down;
        if (PrefabUtility.IsPartOfPrefabInstance(farmer)) PrefabUtility.RecordPrefabInstancePropertyModifications(farmer);
        if (PrefabUtility.IsPartOfPrefabInstance(animator.transform.parent)) PrefabUtility.RecordPrefabInstancePropertyModifications(animator.transform.parent);
        data.FindProperty("farmerVisual").objectReferenceValue = animator.transform.parent;
        data.FindProperty("farmerAnimator").objectReferenceValue = animator;
        data.FindProperty("plantingSeconds").floatValue = PlayerClip("Planting2").length;
        data.ApplyModifiedPropertiesWithoutUndo();
        if (PrefabUtility.IsPartOfPrefabInstance(menu)) PrefabUtility.RecordPrefabInstancePropertyModifications(menu);
    }

    private static void AlignTutorialCrops()
    {
        var plot = Object.FindFirstObjectByType<FarmPlot>();
        var anchor = (Transform)new SerializedObject(plot).FindProperty("cropVisualAnchor").objectReferenceValue;
        float surface = anchor.InverseTransformPoint(new Vector3(anchor.position.x, plot.transform.Find("SoilMesh").GetComponent<Renderer>().bounds.max.y, anchor.position.z)).y;
        var crops = new SerializedObject(Object.FindFirstObjectByType<FarmPlotContextMenu>()).FindProperty("availableCrops");
        var paths = new System.Collections.Generic.HashSet<string>();
        for (int index = 0; index < crops.arraySize; index++)
        {
            var crop = (CropData)crops.GetArrayElementAtIndex(index).objectReferenceValue;
            if (crop == null) continue;
            foreach (var prefab in new[] { crop.plantedPrefab, crop.growingPrefab, crop.grownPrefab, crop.deadPrefab })
            {
                if (prefab == null || !paths.Add(AssetDatabase.GetAssetPath(prefab))) continue;
                string path = AssetDatabase.GetAssetPath(prefab);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    root.transform.localPosition = Vector3.zero;
                    root.transform.localRotation = Quaternion.Euler(0f, root.transform.localEulerAngles.y, 0f);
                    if (root.transform.childCount == 0)
                        root.transform.position += Vector3.up * (surface - FarmCropPrefabRefresh.BoundsOf(root).min.y);
                    foreach (Transform child in root.transform)
                    {
                        if (child.GetComponentsInChildren<Renderer>(true).Length == 0) continue;
                        child.localRotation = Quaternion.Euler(0f, child.localEulerAngles.y, 0f);
                        child.position += Vector3.up * (surface - FarmCropPrefabRefresh.BoundsOf(child.gameObject).min.y);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(child);
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }
    }

    public static void Inspect()
    {
        Directory.CreateDirectory(Reports);
        EditorSceneManager.OpenScene(Root + "Scenes/MainLevel.unity");
        SessionState.SetString("FarmSystemAuditReport", Reports);
        var plot = Object.FindFirstObjectByType<FarmPlot>();
        var soil = plot.transform.Find("SoilMesh");
        var ground = GameObject.Find("Ground").GetComponent<Renderer>();
        var report = new StringBuilder();
        report.AppendLine("Ground top=" + ground.bounds.max.y + "; soil bounds=" + soil.GetComponent<Renderer>().bounds);
        report.AppendLine("Lowest upward face=" + MinimumSurfaceHeight(soil));
        var barn = GameObject.Find("FarmBarn");
        foreach (int angle in new[] { 0, 90, 180, 270 })
        {
            barn.transform.rotation = Quaternion.Euler(0f, angle, 0f);
            FarmSystemChecks.Capture("BarnAngle" + angle);
        }
        foreach (var renderer in Object.FindFirstObjectByType<PlayerFarmController>().GetComponentsInChildren<Renderer>())
        {
            var source = renderer.sharedMaterial;
            report.AppendLine(renderer.name + " material=" + source.name + " shader=" + source.shader.name);
            if (!source.HasProperty("_Tint")) continue;
            report.AppendLine("Tint=" + source.GetColor("_Tint"));
            var preview = new Material(source);
            preview.SetColor("_Tint", new Color32(236, 181, 139, 255));
            renderer.sharedMaterial = preview;
        }
        barn.transform.rotation = Quaternion.identity;
        FarmSystemChecks.Capture("PlayerTintPreview");
        File.WriteAllText(Reports + "Inspection.txt", report.ToString());
    }

    public static float MinimumSurfaceHeight(Transform soil)
    {
        var mesh = soil.GetComponent<MeshFilter>().sharedMesh;
        using (var snapshot = MeshUtility.AcquireReadOnlyMeshData(mesh))
        using (var vertices = new NativeArray<Vector3>(snapshot[0].vertexCount, Allocator.Temp))
        {
            snapshot[0].GetVertices(vertices);
            float minimum = float.PositiveInfinity;
            for (int submesh = 0; submesh < snapshot[0].subMeshCount; submesh++)
            using (var indices = new NativeArray<int>(snapshot[0].GetSubMesh(submesh).indexCount, Allocator.Temp))
            {
                snapshot[0].GetIndices(indices, submesh);
                for (int index = 0; index < indices.Length; index += 3)
                {
                    var a = soil.TransformPoint(vertices[indices[index]]);
                    var b = soil.TransformPoint(vertices[indices[index + 1]]);
                    var c = soil.TransformPoint(vertices[indices[index + 2]]);
                    if (Vector3.Cross(b - a, c - a).normalized.y < 0.4f) continue;
                    minimum = Mathf.Min(minimum, a.y, b.y, c.y);
                }
            }
            return minimum;
        }
    }
}
