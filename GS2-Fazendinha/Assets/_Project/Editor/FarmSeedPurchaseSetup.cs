using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class FarmSeedPurchaseSetup
{
    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainLevel.unity");
        var hud = Object.FindFirstObjectByType<FarmHUD>();
        var data = new SerializedObject(hud);
        var single = (Button)data.FindProperty("buySeedsButton").objectReferenceValue;
        var parent = single.transform.parent;
        var existing = parent.Find("BuyFiveSeedsButton");
        var bulk = existing != null
            ? existing.GetComponent<Button>()
            : Object.Instantiate(single, parent, false);
        bulk.name = "BuyFiveSeedsButton";

        var singleRect = (RectTransform)single.transform;
        var bulkRect = (RectTransform)bulk.transform;
        singleRect.sizeDelta = new Vector2(300f, 48f);
        bulkRect.anchorMin = singleRect.anchorMin;
        bulkRect.anchorMax = singleRect.anchorMax;
        bulkRect.pivot = singleRect.pivot;
        bulkRect.anchoredPosition = singleRect.anchoredPosition + new Vector2(312f, 0f);
        bulkRect.sizeDelta = new Vector2(188f, 48f);

        var singleLabel = single.GetComponentInChildren<TMP_Text>(true);
        var bulkLabel = bulk.GetComponentInChildren<TMP_Text>(true);
        singleLabel.text = "BUY 1 SEED";
        bulkLabel.name = "BuyFiveSeedsButtonLabel";
        bulkLabel.text = "BUY 5 SEEDS";
        FitLabel(singleLabel);
        FitLabel(bulkLabel);

        bulk.onClick = new Button.ButtonClickedEvent();
        UnityEventTools.AddPersistentListener(bulk.onClick, hud.BuyFiveSeeds);
        data.FindProperty("buyFiveSeedsButton").objectReferenceValue = bulk;
        data.FindProperty("seedsPerPurchase").intValue = 1;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        FarmSystemChecks.Run();
    }

    private static void FitLabel(TMP_Text label)
    {
        var rect = label.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
