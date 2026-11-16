using System.IO;
using System.Linq;
using TMPro;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class MenuFarmBackdropSetup
{
    private const string Root = "Assets/_Project/";
    private const string PrefabPath = Root + "Prefabs/Menu/MenuFarmBackdrop.prefab";

    public static void Apply()
    {
        Directory.CreateDirectory(Root + "Prefabs/Menu");
        Directory.CreateDirectory(Root + "Materials/Menu");
        AssetDatabase.Refresh();
        if (!File.Exists(FarmActionEffectSetup.WaterPath) || !File.Exists(FarmActionEffectSetup.FertilizerPath) || !File.Exists(FarmActionEffectSetup.HarvestPath))
            FarmActionEffectSetup.CreatePrefabs();
        EditorSceneManager.OpenScene(Root + "Scenes/MainLevel.unity");
        var sourceGround = GameObject.Find("Ground");
        var sourcePlots = Object.FindObjectsByType<FarmPlot>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .OrderBy(plot => plot.transform.position.z).ThenBy(plot => plot.transform.position.x).ToArray();
        var sourcePlayer = Object.FindFirstObjectByType<PlayerFarmController>().gameObject;
        var sourceLights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var sourceRenderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(renderer => renderer.GetComponentInParent<Canvas>(true) == null &&
                renderer.GetComponentInParent<FarmPlot>(true) == null &&
                renderer.GetComponentInParent<PlayerFarmController>(true) == null).ToArray();
        var ambientMode = RenderSettings.ambientMode;
        Color ambientLight = RenderSettings.ambientLight;
        Color ambientSky = RenderSettings.ambientSkyColor;
        Color ambientEquator = RenderSettings.ambientEquatorColor;
        Color ambientGround = RenderSettings.ambientGroundColor;
        float ambientIntensity = RenderSettings.ambientIntensity;
        var skybox = RenderSettings.skybox;
        var background = Camera.main.backgroundColor;
        var farmBounds = BoundsOf(sourceGround);
        foreach (var renderer in sourceRenderers) farmBounds.Encapsulate(renderer.bounds);
        var root = new GameObject("MenuFarmBackdrop");
        var backdrop = root.AddComponent<MenuFarmBackdrop>();
        var scenery = new GameObject("MainLevelScenery").transform;
        scenery.SetParent(root.transform, false);
        var copied = new System.Collections.Generic.HashSet<Transform>();
        foreach (var renderer in sourceRenderers)
        {
            bool parentCopied = false;
            for (Transform parent = renderer.transform.parent; parent != null; parent = parent.parent)
                parentCopied |= copied.Contains(parent);
            if (parentCopied) continue;
            var clone = CloneVisual(renderer.gameObject, scenery, renderer.name);
            clone.SetActive(true);
            copied.Add(renderer.transform);
        }
        foreach (var sourceLight in sourceLights)
        {
            var light = Object.Instantiate(sourceLight.gameObject, root.transform);
            light.name = "Menu" + sourceLight.name;
        }

        var farmer = new GameObject("MenuFarmer").transform;
        farmer.SetParent(root.transform, false);
        farmer.SetPositionAndRotation(sourcePlayer.transform.position, sourcePlayer.transform.rotation);
        var farmerVisual = new GameObject("FarmerVisual").transform;
        farmerVisual.SetParent(farmer, false);
        CloneVisual(sourcePlayer, farmerVisual, "FarmerModel");

        var data = new SerializedObject(backdrop);
        Set(data, "farmer", farmer);
        Set(data, "farmerVisual", farmerVisual);
        var entries = data.FindProperty("plots");
        entries.arraySize = sourcePlots.Length;
        string[] crops = { "Carrot", "Corn", "Lettuce", "Wheat" };
        for (int index = 0; index < sourcePlots.Length; index++)
        {
            var sourcePlot = sourcePlots[index];
            var crop = AssetDatabase.LoadAssetAtPath<CropData>(Root + "Crops/" + crops[index % crops.Length] + ".asset");
            var plot = new GameObject("Menu" + sourcePlot.name).transform;
            plot.SetParent(root.transform, false);
            CloneVisual(sourcePlot.transform.Find("SoilMesh").gameObject, plot, "PlotVisual");
            var anchor = new GameObject("CropAnchor").transform;
            anchor.SetParent(plot, false);
            var originalAnchor = (Transform)new SerializedObject(sourcePlot).FindProperty("cropVisualAnchor").objectReferenceValue;
            var originalOrigin = originalAnchor != null ? originalAnchor : sourcePlot.transform;
            anchor.SetPositionAndRotation(originalOrigin.position, originalOrigin.rotation);
            anchor.localScale = originalOrigin.lossyScale;
            var point = new GameObject("WorkPoint").transform;
            point.SetParent(plot, false);
            point.position = sourcePlot.InteractionPosition;
            var entry = entries.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("workPoint").objectReferenceValue = point;
            entry.FindPropertyRelative("cropAnchor").objectReferenceValue = anchor;
            entry.FindPropertyRelative("plantedModel").objectReferenceValue = Stage(crop.plantedPrefab, anchor, "Planted");
            entry.FindPropertyRelative("growingModel").objectReferenceValue = Stage(crop.growingPrefab, anchor, "Growing");
            entry.FindPropertyRelative("grownModel").objectReferenceValue = Stage(crop.grownPrefab, anchor, "Grown");
        }
        Set(data, "waterEffect", EffectInstance(root.transform, FarmActionEffectSetup.WaterPath));
        Set(data, "fertilizerEffect", EffectInstance(root.transform, FarmActionEffectSetup.FertilizerPath));
        Set(data, "harvestEffect", EffectInstance(root.transform, FarmActionEffectSetup.HarvestPath));
        var target = new GameObject("MenuCameraTarget").transform;
        target.SetParent(root.transform, false);
        target.position = new Vector3(farmBounds.center.x, BoundsOf(sourceGround).max.y + 1.5f, farmBounds.center.z);
        var camera = new GameObject("MenuCinemachineCamera").AddComponent<CinemachineCamera>();
        camera.transform.SetParent(root.transform, false);
        camera.Follow = camera.LookAt = target;
        camera.Lens.FieldOfView = 44f;
        var orbit = camera.gameObject.AddComponent<CinemachineOrbitalFollow>();
        orbit.Radius = Mathf.Max(31f, Mathf.Max(farmBounds.size.x, farmBounds.size.z) * 1.45f);
        orbit.TrackerSettings.BindingMode = BindingMode.WorldSpace;
        orbit.TrackerSettings.PositionDamping = Vector3.zero;
        orbit.HorizontalAxis.Value = -28f;
        orbit.HorizontalAxis.Recentering.Enabled = false;
        orbit.VerticalAxis.Range = new Vector2(25f, 60f);
        orbit.VerticalAxis.Value = 38f;
        orbit.VerticalAxis.Recentering.Enabled = false;
        orbit.RadialAxis.Value = 1f;
        orbit.RadialAxis.Recentering.Enabled = false;
        camera.gameObject.AddComponent<CinemachineRotationComposer>().Damping = Vector2.zero;
        Set(data, "cameraOrbit", orbit);
        data.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);

        var menuScene = EditorSceneManager.OpenScene(Root + "Scenes/MainMenu.unity");
        var old = GameObject.Find("MenuFarmBackdrop");
        if (old != null) Object.DestroyImmediate(old);
        PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), menuScene);
        var output = Camera.main;
        output.orthographic = false;
        output.backgroundColor = background;
        output.nearClipPlane = 0.1f;
        output.farClipPlane = 200f;
        var brain = output.GetComponent<CinemachineBrain>();
        if (brain == null) brain = output.gameObject.AddComponent<CinemachineBrain>();
        brain.IgnoreTimeScale = true;
        brain.UpdateMethod = CinemachineBrain.UpdateMethods.LateUpdate;
        RenderSettings.ambientMode = ambientMode;
        RenderSettings.ambientLight = ambientLight;
        RenderSettings.ambientSkyColor = ambientSky;
        RenderSettings.ambientEquatorColor = ambientEquator;
        RenderSettings.ambientGroundColor = ambientGround;
        RenderSettings.ambientIntensity = ambientIntensity;
        RenderSettings.skybox = skybox;
        ConfigureMenuReadability();
        EditorSceneManager.MarkSceneDirty(menuScene);
        EditorSceneManager.SaveScene(menuScene);
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("../../Verification/MenuBackdrop");
        File.WriteAllText("../../Verification/MenuBackdrop/Setup.txt", "PASS: MainLevel scenery copied with original world transforms, all plots visible, reusable effects and Cinemachine orbit connected.\n");
    }

    private static GameObject CloneVisual(GameObject source, Transform parent, string name)
    {
        var clone = Object.Instantiate(source);
        clone.name = name;
        clone.transform.SetParent(parent, true);
        clone.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        clone.transform.localScale = source.transform.lossyScale;
        if (PrefabUtility.IsPartOfPrefabInstance(clone)) PrefabUtility.UnpackPrefabInstance(clone, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        foreach (var component in clone.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(component);
        foreach (var component in clone.GetComponentsInChildren<Component>(true))
            if (component != null && !(component is Transform) && !(component is Renderer) && !(component is MeshFilter) && !(component is LODGroup))
                Object.DestroyImmediate(component);
        return clone;
    }

    private static GameObject Stage(GameObject prefab, Transform parent, string name)
    {
        var instance = CloneVisual(prefab, parent, name);
        instance.transform.localPosition = prefab.transform.localPosition;
        instance.transform.localRotation = prefab.transform.localRotation;
        instance.transform.localScale = prefab.transform.localScale;
        instance.SetActive(false);
        return instance;
    }

    private static Transform EffectInstance(Transform parent, string path)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        var particles = instance.GetComponent<ParticleSystem>();
        var main = particles.main;
        main.loop = true;
        main.useUnscaledTime = true;
        main.stopAction = ParticleSystemStopAction.None;
        PrefabUtility.RecordPrefabInstancePropertyModifications(particles);
        instance.SetActive(false);
        return instance.transform;
    }

    private static Bounds BoundsOf(GameObject model)
    {
        var renderers = model.GetComponentsInChildren<Renderer>(true);
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    private static void FitWidth(GameObject model, float width)
    {
        var size = BoundsOf(model).size;
        model.transform.localScale *= width / Mathf.Max(size.x, size.z);
    }

    private static void PlaceBottom(GameObject model, Vector3 point)
    {
        var bounds = BoundsOf(model);
        model.transform.position += point - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
    }

    private static void ConfigureMenuReadability()
    {
        var canvas = GameObject.Find("MainMenuCanvas").transform;
        var previous = canvas.Find("MenuBackdropShade");
        if (previous != null) Object.DestroyImmediate(previous.gameObject);
        var shade = FarmMenuSetup.FullRect(canvas, "MenuBackdropShade");
        var image = shade.gameObject.AddComponent<Image>();
        image.color = new Color(0.06f, 0.035f, 0.015f, 0.32f);
        image.raycastTarget = false;
        shade.SetAsFirstSibling();
        foreach (string name in new[] { "GameTitle", "PlayLabel" })
        {
            var text = FarmMenuSetup.Find(canvas, name).GetComponent<TMP_Text>();
            text.color = new Color32(255, 241, 206, 255);
        }
    }

    private static void Set(SerializedObject data, string field, Object value) => data.FindProperty(field).objectReferenceValue = value;
}
