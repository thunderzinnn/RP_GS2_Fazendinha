using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FarmActionEffectSetup
{
    private const string Root = "Assets/_Project/";
    public const string WaterPath = Root + "Prefabs/Effects/WaterDroplets.prefab";
    public const string FertilizerPath = Root + "Prefabs/Effects/FertilizerBoost.prefab";
    public const string HarvestPath = Root + "Prefabs/Effects/HarvestSparkles.prefab";

    public static void ConnectGameplay()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(HarvestPath) == null)
            Create(true, HarvestPath, new Color32(255, 205, 66, 255), true);
        var water = AssetDatabase.LoadAssetAtPath<GameObject>(WaterPath);
        var fertilizer = AssetDatabase.LoadAssetAtPath<GameObject>(FertilizerPath);
        var harvest = AssetDatabase.LoadAssetAtPath<GameObject>(HarvestPath);
        if (water == null || fertilizer == null || water.GetComponent<ParticleSystem>() == null || fertilizer.GetComponent<ParticleSystem>() == null)
            throw new System.InvalidOperationException("Native action effect prefabs are missing.");
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { Root + "Prefabs" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool changed = false;
                foreach (var plot in root.GetComponentsInChildren<FarmPlot>(true))
                    changed |= ConnectPlot(plot, water, fertilizer, harvest);
                if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { Root + "Scenes" }))
        {
            var scene = EditorSceneManager.OpenScene(AssetDatabase.GUIDToAssetPath(guid));
            bool changed = false;
            foreach (var plot in Object.FindObjectsByType<FarmPlot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                changed |= ConnectPlot(plot, water, fertilizer, harvest);
            if (!changed) continue;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        ConfigureMenuEffects();
        FarmPolishChecks.Run();
    }

    private static bool ConnectPlot(FarmPlot plot, GameObject water, GameObject fertilizer, GameObject harvest)
    {
        var data = new SerializedObject(plot);
        var waterField = data.FindProperty("wateringEffectPrefab");
        var fertilizerField = data.FindProperty("fertilizerEffectPrefab");
        var harvestField = data.FindProperty("harvestEffectPrefab");
        if (waterField.objectReferenceValue == water && fertilizerField.objectReferenceValue == fertilizer && harvestField.objectReferenceValue == harvest) return false;
        waterField.objectReferenceValue = water;
        fertilizerField.objectReferenceValue = fertilizer;
        harvestField.objectReferenceValue = harvest;
        data.ApplyModifiedPropertiesWithoutUndo();
        if (PrefabUtility.IsPartOfPrefabInstance(plot)) PrefabUtility.RecordPrefabInstancePropertyModifications(plot);
        return true;
    }

    public static void ApplyAndCheck()
    {
        Apply();
        FarmPolishChecks.Run();
    }

    public static void Apply()
    {
        CreatePrefabs();
        foreach (string name in new[] { "MainLevel", "Tutorial" })
        {
            var scene = EditorSceneManager.OpenScene(Root + "Scenes/" + name + ".unity");
            foreach (var plot in Object.FindObjectsByType<FarmPlot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var data = new SerializedObject(plot);
                data.FindProperty("wateringEffectPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(WaterPath);
                data.FindProperty("fertilizerEffectPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(FertilizerPath);
                data.FindProperty("harvestEffectPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(HarvestPath);
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(plot);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        ConfigureMenuEffects();
    }

    public static void CreatePrefabs()
    {
        Directory.CreateDirectory(Root + "Prefabs/Effects");
        Directory.CreateDirectory(Root + "Materials/Effects");
        AssetDatabase.Refresh();
        Create(false, WaterPath, new Color32(77, 199, 250, 255));
        Create(true, FertilizerPath, new Color32(137, 255, 151, 255));
        Create(true, HarvestPath, new Color32(255, 205, 66, 255), true);
        AssetDatabase.SaveAssets();
    }

    private static void Create(bool rising, string path, Color color, bool harvest = false)
    {
        var root = new GameObject(harvest ? "HarvestSparkles" : rising ? "FertilizerBoost" : "WaterDroplets");
        try
        {
            var particles = root.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.duration = harvest ? 1.2f : rising ? 1.4f : 1.1f;
            main.loop = false;
            main.playOnAwake = true;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = harvest ? 1.1f : rising ? 1.35f : 0.85f;
            main.startSpeed = 0f;
            main.startSize = harvest ? new ParticleSystem.MinMaxCurve(0.12f, 0.22f) : rising ? new ParticleSystem.MinMaxCurve(0.28f, 0.42f) : new ParticleSystem.MinMaxCurve(0.10f, 0.16f);
            main.startColor = color;
            main.maxParticles = 32;
            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, harvest ? (short)10 : rising ? (short)8 : (short)14) });
            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.65f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            shape.position = Vector3.up * (rising ? 0.3f : 1.8f);
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            float verticalSpeed = rising ? 1.05f : -1.65f;
            velocity.y = new ParticleSystem.MinMaxCurve(verticalSpeed, verticalSpeed);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.35f), new Keyframe(0.18f, 1f), new Keyframe(0.75f, 0.85f), new Keyframe(1f, 0.2f)));
            var fade = particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(1f, 0.65f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            string materialPath = Root + "Materials/Effects/" + root.name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = 3000;
            EditorUtility.SetDirty(material);
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.alignment = ParticleSystemRenderSpace.View;
            if (harvest)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                renderer.mesh = cube.GetComponent<MeshFilter>().sharedMesh;
                Object.DestroyImmediate(cube);
            }
            else renderer.mesh = CreateMesh(rising);
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static Mesh CreateMesh(bool rising)
    {
        string folder = Root + "Models/Effects";
        Directory.CreateDirectory(folder);
        AssetDatabase.Refresh();
        string path = folder + (rising ? "/FertilizerPlus.asset" : "/WaterDrop.asset");
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, path); }
        mesh.Clear();
        mesh.name = rising ? "FertilizerPlus" : "WaterDrop";
        var vertices = new System.Collections.Generic.List<Vector3>();
        var triangles = new System.Collections.Generic.List<int>();
        Vector2[] outline = rising ? new[] {
            new Vector2(-0.14f, 0.5f), new Vector2(0.14f, 0.5f), new Vector2(0.14f, 0.14f),
            new Vector2(0.5f, 0.14f), new Vector2(0.5f, -0.14f), new Vector2(0.14f, -0.14f),
            new Vector2(0.14f, -0.5f), new Vector2(-0.14f, -0.5f), new Vector2(-0.14f, -0.14f),
            new Vector2(-0.5f, -0.14f), new Vector2(-0.5f, 0.14f), new Vector2(-0.14f, 0.14f)
        } : new[] {
            new Vector2(0f, 0.85f), new Vector2(0.35f, 0.15f), new Vector2(0.45f, -0.2f),
            new Vector2(0.3f, -0.45f), new Vector2(0f, -0.55f), new Vector2(-0.3f, -0.45f),
            new Vector2(-0.45f, -0.2f), new Vector2(-0.35f, 0.15f)
        };
        vertices.Add(Vector3.zero);
        foreach (var point in outline) vertices.Add(new Vector3(point.x, point.y, 0f));
        for (int index = 0; index < outline.Length; index++)
        { triangles.Add(0); triangles.Add(index + 1); triangles.Add((index + 1) % outline.Length + 1); }
        mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
        var colors = new Color[vertices.Count];
        for (int index = 0; index < colors.Length; index++) colors[index] = Color.white;
        mesh.colors = colors;
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    private static void ConfigureMenuEffects()
    {
        string path = Root + "Prefabs/Menu/MenuFarmBackdrop.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            ConfigureMenuInstance(root.GetComponent<MenuFarmBackdrop>());
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        var scene = EditorSceneManager.OpenScene(Root + "Scenes/MainMenu.unity");
        ConfigureMenuInstance(Object.FindFirstObjectByType<MenuFarmBackdrop>());
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void ConfigureMenuInstance(MenuFarmBackdrop backdrop)
    {
        var data = new SerializedObject(backdrop);
        var old = (Transform)data.FindProperty("harvestEffect").objectReferenceValue;
        if (old == null || old.GetComponent<ParticleSystem>() == null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(HarvestPath), backdrop.transform);
            instance.name = "HarvestEffect";
            if (old != null)
            {
                instance.transform.SetParent(old.parent, false);
                instance.transform.localPosition = old.localPosition;
                instance.transform.localRotation = old.localRotation;
                instance.transform.localScale = old.localScale;
                instance.transform.SetSiblingIndex(old.GetSiblingIndex());
                Object.DestroyImmediate(old.gameObject);
            }
            data.FindProperty("harvestEffect").objectReferenceValue = instance.transform;
            data.ApplyModifiedPropertiesWithoutUndo();
            if (PrefabUtility.IsPartOfPrefabInstance(backdrop)) PrefabUtility.RecordPrefabInstancePropertyModifications(backdrop);
        }
        foreach (string name in new[] { "waterEffect", "fertilizerEffect", "harvestEffect" })
        {
            var effect = (Transform)data.FindProperty(name).objectReferenceValue;
            var particles = effect.GetComponent<ParticleSystem>();
            var main = particles.main;
            main.loop = true; main.useUnscaledTime = true;
            main.stopAction = ParticleSystemStopAction.None;
            PrefabUtility.RecordPrefabInstancePropertyModifications(particles);
            effect.gameObject.SetActive(false);
        }
    }
}
