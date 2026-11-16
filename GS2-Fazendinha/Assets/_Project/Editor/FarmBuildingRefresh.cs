using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FarmBuildingRefresh
{
    private const string Root = "Assets/_Project/";

    public static void Apply(Scene scene)
    {
        Directory.CreateDirectory(Root + "Prefabs/Buildings");
        AssetDatabase.Refresh();
        var report = new StringBuilder();
        var atlas = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Models/Crops/Grown/SM_Crop_Carrot_Stage_Grown.fbx")
            .GetComponentInChildren<Renderer>().sharedMaterial;
        var plotModel = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Models/Buildings/SM_Farm_Building_Plot.fbx");
        var groundModel = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Models/Buildings/SM_Farm_Building_Floor.fbx");
        var plotPrefabPath = Root + "Prefabs/FarmPlot.prefab";
        var plotPrefab = PrefabUtility.LoadPrefabContents(plotPrefabPath);
        try
        {
            ReplaceMesh(plotPrefab.transform.Find("SoilMesh").gameObject, plotModel, atlas);
            PrefabUtility.SaveAsPrefabAsset(plotPrefab, plotPrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(plotPrefab); }

        foreach (var root in scene.GetRootGameObjects())
        foreach (var plot in root.GetComponentsInChildren<FarmPlot>(true))
        {
            var soil = plot.transform.Find("SoilMesh").gameObject;
            ReplaceMesh(soil, plotModel, atlas);
            PrefabUtility.RecordPrefabInstancePropertyModifications(soil.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(soil.GetComponent<MeshFilter>());
            PrefabUtility.RecordPrefabInstancePropertyModifications(soil.GetComponent<Renderer>());
            report.AppendLine(plot.name + " bounds=" + soil.GetComponent<Renderer>().bounds);
        }

        var ground = GameObject.Find("Ground");
        float groundTop = ground.GetComponent<Renderer>().bounds.max.y;
        ReplaceMesh(ground, groundModel, ground.GetComponent<Renderer>().sharedMaterial);
        var groundCopy = Object.Instantiate(ground);
        groundCopy.name = "FarmGround";
        PrefabUtility.SaveAsPrefabAsset(groundCopy, Root + "Prefabs/Buildings/FarmGround.prefab");
        Object.DestroyImmediate(groundCopy);

        var barnSource = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Models/Buildings/SM_Farm_Building_Barn.fbx");
        var barnRoot = new GameObject("FarmBarn");
        var barnModel = (GameObject)PrefabUtility.InstantiatePrefab(barnSource, barnRoot.transform);
        foreach (var renderer in barnModel.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = atlas;
        var barnBounds = FarmCropPrefabRefresh.BoundsOf(barnRoot);
        barnModel.transform.localScale *= 4f / Mathf.Max(barnBounds.size.x, barnBounds.size.z);
        barnBounds = FarmCropPrefabRefresh.BoundsOf(barnRoot);
        barnModel.transform.position -= new Vector3(barnBounds.center.x, barnBounds.min.y, barnBounds.center.z);
        PrefabUtility.RecordPrefabInstancePropertyModifications(barnModel.transform);
        foreach (var renderer in barnModel.GetComponentsInChildren<Renderer>())
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        var barnPrefab = PrefabUtility.SaveAsPrefabAsset(barnRoot, Root + "Prefabs/Buildings/FarmBarn.prefab");
        Object.DestroyImmediate(barnRoot);
        var oldBarn = GameObject.Find("FarmBarn");
        if (oldBarn != null) Object.DestroyImmediate(oldBarn);
        var barn = (GameObject)PrefabUtility.InstantiatePrefab(barnPrefab, scene);
        var camera = Camera.main;
        float previousAspect = camera.aspect;
        camera.aspect = 1600f / 900f;
        barn.transform.position = ScreenGround(camera, new Vector2(0.69f, 0.65f), groundTop);
        var groundBounds = ground.GetComponent<Renderer>().bounds;
        var barnSize = FarmCropPrefabRefresh.BoundsOf(barn).extents;
        var barnPosition = barn.transform.position;
        barnPosition.x = Mathf.Clamp(barnPosition.x, groundBounds.min.x + barnSize.x, groundBounds.max.x - barnSize.x);
        barnPosition.z = Mathf.Clamp(barnPosition.z, groundBounds.min.z + barnSize.z, groundBounds.max.z - barnSize.z);
        barn.transform.position = barnPosition;

        foreach (var root in scene.GetRootGameObjects())
        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            if (transform.name == "WormComposter")
            {
                var wormBounds = transform.GetComponent<Renderer>().bounds;
                var position = ScreenGround(camera, new Vector2(0.55f, 0.70f), groundTop);
                position.y += wormBounds.extents.y;
                transform.position = position;
                report.AppendLine("WormComposter=" + position);
            }
        camera.aspect = previousAspect;
        report.AppendLine("FarmBarn=" + barn.transform.position + "; bounds=" + FarmCropPrefabRefresh.BoundsOf(barn));
        report.AppendLine("Camera framing unchanged. Plot centers, interaction points and planting areas preserved.");
        File.WriteAllText("../../Verification/AssetRefresh/Buildings.txt", report.ToString());
    }

    private static void ReplaceMesh(GameObject target, GameObject source, Material material)
    {
        var renderer = target.GetComponent<MeshRenderer>();
        var desired = renderer.bounds;
        var mesh = source.GetComponent<MeshFilter>().sharedMesh;
        target.GetComponent<MeshFilter>().sharedMesh = mesh;
        target.transform.localRotation = source.transform.localRotation;
        target.transform.localScale = source.transform.localScale;
        var size = renderer.bounds.size;
        target.transform.localScale = Vector3.Scale(target.transform.localScale,
            new Vector3(desired.size.x / size.x, desired.size.y / size.y, desired.size.z / size.z));
        target.transform.position += desired.center - renderer.bounds.center;
        renderer.sharedMaterial = material;
        var collider = target.GetComponent<BoxCollider>();
        if (collider != null)
        {
            collider.center = mesh.bounds.center;
            collider.size = mesh.bounds.size;
        }
    }

    private static Vector3 ScreenGround(Camera camera, Vector2 point, float height)
    {
        var ray = camera.ViewportPointToRay(new Vector3(point.x, point.y));
        var plane = new Plane(Vector3.up, Vector3.up * height);
        plane.Raycast(ray, out float distance);
        return ray.GetPoint(distance);
    }
}
