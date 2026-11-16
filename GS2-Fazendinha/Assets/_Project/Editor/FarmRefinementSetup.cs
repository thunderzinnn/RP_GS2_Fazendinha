using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FarmRefinementSetup
{
    private const string Root = "Assets/_Project/";

    public static void Apply()
    {
        Directory.CreateDirectory("../../Verification/Refinement");
        foreach (string name in new[] { "MainLevel", "Tutorial" })
        {
            var scene = EditorSceneManager.OpenScene(Root + "Scenes/" + name + ".unity");
            ApplyHUD(GameObject.Find("Main Canvas").transform);
            if (name == "MainLevel") MoveBarnRight();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        FarmSystemChecks.RunRefinement();
    }

    public static void ApplyHUD(Transform canvas)
    {
        ScaleIndicator(canvas, "WaterSlider", 80f);
        ScaleIndicator(canvas, "FertilizerSlider", 214f);
        ScaleIndicator(canvas, "WaterRegenerationTimerText", 164f);
        ScaleIndicator(canvas, "FertilizerGenerationTimerText", 259f);
        foreach (string name in new[] { "WaterRegenerationTimerText", "FertilizerGenerationTimerText" })
            FarmMenuSetup.Find(canvas, name).GetComponent<TMP_Text>().fontSize = 20f;
        var pause = (RectTransform)FarmMenuSetup.Find(canvas, "PauseButton");
        pause.anchorMin = pause.anchorMax = pause.pivot = Vector2.one;
        pause.anchoredPosition = new Vector2(-24f, -12f);
        pause.sizeDelta = new Vector2(52f, 52f);
        var icon = (RectTransform)pause.Find("Icon");
        icon.offsetMin = new Vector2(12f, 12f);
        icon.offsetMax = new Vector2(-12f, -12f);
    }

    private static void ScaleIndicator(Transform canvas, string name, float top)
    {
        var rect = (RectTransform)FarmMenuSetup.Find(canvas, name);
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
        rect.anchoredPosition = new Vector2(-24f, -top);
        rect.localScale = Vector3.one * 0.75f;
    }

    public static void MoveBarnRight()
    {
        var barn = GameObject.Find("FarmBarn");
        if (barn == null) return;
        var camera = Camera.main;
        camera.aspect = 1600f / 900f;
        var ground = GameObject.Find("Ground").GetComponent<Renderer>().bounds;
        int steps = 0;
        while (camera.WorldToViewportPoint(FarmCropPrefabRefresh.BoundsOf(barn).center).x < 0.78f && steps++ < 100)
        {
            var bounds = FarmCropPrefabRefresh.BoundsOf(barn);
            if (bounds.max.x + 0.1f > ground.max.x - 0.1f) break;
            barn.transform.position += Vector3.right * 0.1f;
        }
        PrefabUtility.RecordPrefabInstancePropertyModifications(barn.transform);
        camera.ResetAspect();
    }
}
