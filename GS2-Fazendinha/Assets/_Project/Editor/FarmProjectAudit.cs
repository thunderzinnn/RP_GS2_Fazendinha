using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public static class FarmProjectAudit
{
    private const string ScenePath = "Assets/_Project/Scenes/MainLevel.unity";

    public static void VerifyRules()
    {
        var report = new StringBuilder();
        int errors = 0;
        foreach (string name in new[] { "MainMenu", "Tutorial", "MainLevel", "Testes", "Other" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/" + name + ".unity");
            foreach (var root in scene.GetRootGameObjects()) AuditRoot(root, report, ref errors);
        }
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var root = PrefabUtility.LoadPrefabContents(path);
            try { AuditRoot(root, report, ref errors, true); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        foreach (string path in new[] { FarmActionEffectSetup.WaterPath, FarmActionEffectSetup.FertilizerPath, FarmActionEffectSetup.HarvestPath })
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var effect = root.GetComponent<ParticleSystem>();
            Check(effect != null && root.GetComponents<MonoBehaviour>().Length == 0,
                root.name + ": native particles without animation scripts", report, ref errors);
            if (effect != null)
                Check(!effect.main.loop && effect.main.stopAction == ParticleSystemStopAction.Destroy,
                    root.name + ": one-shot with native cleanup", report, ref errors);
        }
        report.AppendLine("Errors=" + errors);
        Directory.CreateDirectory("../../Verification/CodeReview");
        File.WriteAllText("../../Verification/CodeReview/RulesAudit.txt", report.ToString());
        if (errors > 0) throw new System.InvalidOperationException("Project rules audit failed: " + errors);
        FarmDeliveryReviewChecks.Run();
    }

    private static void AuditRoot(GameObject root, StringBuilder report, ref int errors, bool prefab = false)
    {
        foreach (var node in root.GetComponentsInChildren<Transform>(true))
        {
            string path = GetPath(node);
            Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject) == 0,
                path + ": no missing scripts", report, ref errors);
            foreach (var behaviour in node.GetComponents<MonoBehaviour>())
            {
                if (behaviour == null) continue;
                var data = new SerializedObject(behaviour);
                var property = data.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.String || property.name != "m_MethodName" || string.IsNullOrEmpty(property.stringValue)) continue;
                    int separator = property.propertyPath.LastIndexOf('.');
                    var target = data.FindProperty(property.propertyPath.Substring(0, separator) + ".m_Target");
                    bool found = false;
                    if (target != null && target.objectReferenceValue != null)
                        foreach (var method in target.objectReferenceValue.GetType().GetMethods(
                            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
                            if (method.Name == property.stringValue) { found = true; break; }
                    if (!found && prefab && target != null && target.objectReferenceValue == null)
                        report.AppendLine("WARNING: " + path + ": unbound prefab event " + property.stringValue);
                    else Check(found, path + ": event " + property.stringValue, report, ref errors);
                }
            }
        }
        Check(root.GetComponentsInChildren<Text>(true).Length == 0, root.name + ": no legacy Text", report, ref errors);
        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            Check(!text.raycastTarget, GetPath(text.transform) + ": text raycast off", report, ref errors);
        foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
        {
            if (!canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace) continue;
            var scaler = canvas.GetComponent<CanvasScaler>();
            Check(scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize,
                GetPath(canvas.transform) + ": Scale With Screen Size", report, ref errors);
            Check(canvas.GetComponentsInChildren<Animator>(true).Length == 0,
                GetPath(canvas.transform) + ": no UI Animator", report, ref errors);
        }
        foreach (var image in root.GetComponentsInChildren<Image>(true))
        {
            var control = image.GetComponentInParent<Selectable>(true);
            bool interactive = (control != null && control.targetGraphic == image) || image.GetComponentInChildren<Selectable>(true) != null ||
                image.name.Contains("Overlay") || image.name.Contains("Blocker");
            foreach (var behaviour in image.GetComponents<MonoBehaviour>())
                if (behaviour is IPointerClickHandler || behaviour is IPointerDownHandler || behaviour is IDragHandler) interactive = true;
            if (!interactive) Check(!image.raycastTarget, GetPath(image.transform) + ": decorative raycast off", report, ref errors);
        }
        foreach (var control in root.GetComponentsInChildren<Selectable>(true))
            if (control.interactable && control.targetGraphic != null)
                Check(control.targetGraphic.raycastTarget, GetPath(control.transform) + ": control raycast on", report, ref errors);
    }

    private static void Check(bool valid, string label, StringBuilder report, ref int errors)
    {
        report.AppendLine((valid ? "PASS: " : "FAIL: ") + label);
        if (!valid) errors++;
    }

    public static void ApplyUIRules()
    {
        RepairPlotCard();
        var report = new StringBuilder();
        int legacy = 0;
        foreach (string name in new[] { "MainMenu", "Tutorial", "MainLevel", "Testes", "Other" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/" + name + ".unity");
            int changed = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                legacy += root.GetComponentsInChildren<Text>(true).Length;
                foreach (var scaler in root.GetComponentsInChildren<CanvasScaler>(true))
                {
                    if (scaler.GetComponent<Canvas>().renderMode == RenderMode.WorldSpace) continue;
                    if (scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize) continue;
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(scaler);
                    changed++;
                }
                foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
                {
                    var control = graphic.GetComponentInParent<Selectable>(true);
                    bool target = control != null && control.targetGraphic == graphic;
                    bool blocker = graphic.name.Contains("Overlay") || graphic.name.Contains("Blocker") ||
                        graphic.GetComponentInChildren<Selectable>(true) != null;
                    bool handler = false;
                    foreach (var behaviour in graphic.GetComponents<MonoBehaviour>())
                        if (behaviour is IPointerClickHandler || behaviour is IPointerDownHandler || behaviour is IDragHandler) handler = true;
                    bool raycast = !(graphic is TMP_Text) && (target || blocker || handler);
                    if (graphic.raycastTarget == raycast) continue;
                    graphic.raycastTarget = raycast;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(graphic);
                    changed++;
                }
                foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                    if (canvas.isRootCanvas)
                        report.AppendLine(name + ": UI Animators=" + canvas.GetComponentsInChildren<Animator>(true).Length);
            }
            if (changed > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            report.AppendLine(name + ": UI properties adjusted=" + changed);
        }
        report.AppendLine("Legacy Text components=" + legacy);
        Directory.CreateDirectory("../../Verification/CodeReview");
        File.WriteAllText("../../Verification/CodeReview/UIRules.txt", report.ToString());
        if (legacy != 0) throw new System.InvalidOperationException("Legacy Text components found.");
        VerifyRules();
    }

    private static void RepairPlotCard()
    {
        const string path = "Assets/_Project/Prefabs/PlotExpansionCard.prefab";
        var card = PrefabUtility.LoadPrefabContents(path);
        try
        {
            foreach (var button in card.GetComponentsInChildren<Button>(true))
                for (int index = button.onClick.GetPersistentEventCount() - 1; index >= 0; index--)
                    if (button.onClick.GetPersistentTarget(index) == null)
                        UnityEditor.Events.UnityEventTools.RemovePersistentListener(button.onClick, index);
            PrefabUtility.SaveAsPrefabAsset(card, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(card); }
        foreach (string name in new[] { "MainLevel", "Tutorial" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/" + name + ".unity");
            bool changed = false;
            foreach (var expansion in Object.FindObjectsByType<FarmPlotExpansion>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var data = new SerializedObject(expansion);
                var button = (Button)data.FindProperty("buyPlotButton").objectReferenceValue;
                if (button == null) continue;
                bool bound = false;
                for (int index = button.onClick.GetPersistentEventCount() - 1; index >= 0; index--)
                {
                    if (button.onClick.GetPersistentTarget(index) == expansion && button.onClick.GetPersistentMethodName(index) == nameof(FarmPlotExpansion.BuyNextPlot)) bound = true;
                    if (button.onClick.GetPersistentTarget(index) != null) continue;
                    UnityEditor.Events.UnityEventTools.RemovePersistentListener(button.onClick, index);
                    changed = true;
                }
                if (!bound)
                {
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, expansion.BuyNextPlot);
                    changed = true;
                }
                if (changed) PrefabUtility.RecordPrefabInstancePropertyModifications(button);
            }
            if (!changed) continue;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    public static void Inspect()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var report = new StringBuilder();
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                var path = GetPath(transform);
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) > 0)
                    report.AppendLine("MISSING SCRIPT: " + path);
                foreach (var behaviour in transform.GetComponents<MonoBehaviour>())
                {
                    if (behaviour == null) continue;
                    if (behaviour.GetType().Assembly.GetName().Name != "Assembly-CSharp") continue;
                    report.AppendLine("SYSTEM " + behaviour.GetType().Name + " | " + path + " | active=" + transform.gameObject.activeInHierarchy);
                    var serialized = new SerializedObject(behaviour);
                    var property = serialized.GetIterator();
                    while (property.NextVisible(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                        report.AppendLine("  " + property.propertyPath + " -> " + (property.objectReferenceValue != null ? property.objectReferenceValue.name : "NULL"));
                    }
                }
                foreach (var button in transform.GetComponents<Button>())
                {
                    report.AppendLine("BUTTON " + path);
                    for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                        report.AppendLine("  " + button.onClick.GetPersistentTarget(i) + " -> " + button.onClick.GetPersistentMethodName(i));
                }
                foreach (var text in transform.GetComponents<TMP_Text>())
                    report.AppendLine("TEXT " + path + " = " + text.text.Replace("\n", " / "));
            }
        }
        Directory.CreateDirectory("../../Verification/SystemAudit");
        File.WriteAllText("../../Verification/SystemAudit/SceneInspection.txt", report.ToString());
        Debug.Log("Farm audit inspection completed.");
    }

    private static string GetPath(Transform transform)
    {
        return transform.parent == null ? transform.name : GetPath(transform.parent) + "/" + transform.name;
    }
}
