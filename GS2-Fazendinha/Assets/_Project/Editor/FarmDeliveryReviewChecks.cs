using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public static class FarmDeliveryReviewChecks
{
    private const string Session = "FarmDeliveryReview";
    private const string Report = "../../Verification/DeliveryPolish/";
    private static readonly StringBuilder results = new StringBuilder();
    private static readonly StringBuilder overflow = new StringBuilder();
    private static int phase;
    private static int screen;
    private static bool pendingPanel;
    private static double started;
    private static Bounds barnBounds;
    private static Vector3 goal;
    private static PlayerFarmController player;

    public static void ApplyAndRun()
    {
        FarmDeliveryPolishSetup.Apply();
        FarmToonPreviewSetup.ApplyBarnPreview();
        FarmToonPreviewSetup.AlignCarrotBases();
        Run();
    }

    public static void Run()
    {
        SessionState.SetBool(Session, true);
        SessionState.SetString("FarmDeliveryPrefix", "GoldenRootTest_Delivery_" + Guid.NewGuid().ToString("N"));
        SessionState.SetString("FarmSystemAuditReport", Report);
        ConfigureProfile();
        PlayerPrefs.SetFloat(FarmSaveManager.KeyPrefix + ".MusicVolume", 0.31f);
        PlayerPrefs.SetFloat(FarmSaveManager.KeyPrefix + ".EffectsVolume", 0.59f);
        PlayerPrefs.SetInt(FarmSaveManager.KeyPrefix + "_Other.Coins", 901);
        PlayerPrefs.Save();
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainMenu.unity");
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    private static void Restore()
    {
        if (!SessionState.GetBool(Session, false)) return;
        ConfigureProfile();
        AudioListener.volume = 0f;
        phase = screen = 0;
        pendingPanel = false;
        started = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
    }

    private static void ConfigureProfile()
    {
        FarmSaveManager.KeyPrefix = SessionState.GetString("FarmDeliveryPrefix", "GoldenRootTest_Delivery");
        FarmSaveManager.AutomaticSavingEnabled = false;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.timeSinceStartup - started < 1d) return;
        try
        {
            if (phase == 0)
            {
                var menus = ObjectOf<FarmUIController>();
                var canvas = menus.transform;
                Require(Find(canvas, "MainMenuBoard") != null && Find(canvas, "GameLogo").GetComponent<Image>().sprite != null,
                    "Menu uses the farm logo and wooden board");
                Require(Read<string>(FarmSaveManager.Instance, "saveKeyPrefix") == FarmDeliveryPolishSetup.PreviewPrefix,
                    "Copy has its own save namespace");
                Capture("MainMenu");
                menus.OpenOptions();
                Capture("Settings");
                PlayerPrefs.SetInt(FarmSaveManager.KeyPrefix + ".Farm.HasSave", 1);
                PlayerPrefs.SetInt(FarmSaveManager.KeyPrefix + ".Farm.Coins", 4242);
                menus.ShowResetProgress();
                Capture("ResetConfirmation");
                menus.CancelResetProgress();
                Require(PlayerPrefs.GetInt(FarmSaveManager.KeyPrefix + ".Farm.Coins") == 4242, "Cancelling reset preserves progress");
                menus.ShowResetProgress();
                menus.ConfirmResetProgress();
                phase = 1;
            }
            else if (phase == 1)
            {
                CheckReset();
                ObjectOf<FarmUIController>().PlayGame();
                phase = 2;
            }
            else if (phase == 2)
            {
                Require(SceneManager.GetActiveScene().name == "Tutorial" && ObjectOf<FarmTutorialController>().CurrentStep == FarmTutorialStep.SelectPlot,
                    "Play still starts a fresh tutorial");
                Require(!Find(ObjectOf<FarmHUD>().transform, "SaveGameButton").gameObject.activeSelf, "Tutorial Save button remains hidden");
                Capture("TutorialHud");
                SceneManager.LoadScene("MainLevel");
                phase = 3;
            }
            else if (phase == 3)
            {
                player = ObjectOf<PlayerFarmController>();
                Require(ObjectOf<FarmWallet>().Coins == ObjectOf<FarmWallet>().StartingCoins && ObjectOf<FarmMissionBoard>().CompletedMissionCount == 0,
                    "Farm starts with default resources and no completed missions");
                var solid = GameObject.Find("FarmBarn").transform.Find("BarnSolidCollider").GetComponent<BoxCollider>();
                Require(solid != null && !solid.isTrigger, "Barn has a solid collider including its door");
                barnBounds = solid.bounds;
                float ground = GameObject.Find("Ground").GetComponent<Renderer>().bounds.max.y;
                Vector3 middle = new Vector3(barnBounds.center.x, ground, barnBounds.center.z);
                Require(!NavMesh.SamplePosition(middle, out _, 0.1f, NavMesh.AllAreas), "Barn footprint is excluded from navigation");
                BeginBarnRoute(ground);
                phase = 4;
            }
            else if (phase == 4)
            {
                Require(!InsideBarn(player.transform.position), "Moving player stays outside the barn footprint");
                Vector3 remaining = player.Agent.nextPosition - goal;
                remaining.y = 0f;
                if (player.Agent.pathPending || remaining.magnitude > 0.7f)
                {
                    if (EditorApplication.timeSinceStartup - started > 30d) throw new Exception("Player did not finish the path around the barn: " + player.Agent.nextPosition + " goal=" + goal);
                    return;
                }
                player.Machine.ChangeState(player.Idle);
                Require(true, "Player completes the route around the barn");
                SeedVisualComparison();
                phase = 5;
            }
            else if (phase == 5)
            {
                ObjectOf<FarmHUD>().RefreshHUD();
                Capture("FarmOriginalMaterials");
                CheckHud();
                screen = 0;
                phase = 6;
            }
            else if (phase == 6)
            {
                if (CaptureNextPanel()) return;
                FarmToonPreviewSetup.ApplyToScene(SceneManager.GetActiveScene());
                phase = 7;
            }
            else if (phase == 7)
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Project/Shaders/FarmToonPreview.shadergraph");
                var messages = ShaderUtil.GetShaderMessages(shader);
                File.WriteAllText(Report + "ShaderMessages.txt", string.Join("\n", messages.Select(message => message.severity + ": " + message.message)));
                Require(!messages.Any(message => message.severity.ToString() == "Error"), "Toon preview compiles without shader errors");
                Capture("FarmToonPreview");
                FarmSaveManager.Instance.SaveGame();
                ObjectOf<FarmUIController>().ReturnToMainMenu();
                phase = 8;
            }
            else if (phase == 8)
            {
                Require(PlayerPrefs.GetInt(FarmSaveManager.KeyPrefix + ".Farm.HasSave") == 1, "Return to Main Menu saves the farm");
                var menus = ObjectOf<FarmUIController>();
                menus.OpenOptions();
                menus.ShowResetProgress();
                menus.ConfirmResetProgress();
                phase = 9;
            }
            else if (phase == 9)
            {
                CheckReset();
                SceneManager.LoadScene("MainLevel");
                phase = 10;
            }
            else if (phase == 10)
            {
                Require(ObjectOf<FarmWallet>().Coins == ObjectOf<FarmWallet>().StartingCoins && ObjectOf<FarmPlotExpansion>().PurchasedPlots == 0,
                    "Reset removes saved money and plot purchases");
                Require(ObjectOf<FarmMissionBoard>().CompletedMissionCount == 0 && ObjectOf<FarmTechnologyManager>().GetUnlockedNames() == "None",
                    "Reset removes mission and technology progress");
                Require(ObjectOf<FarmWaterTank>().CurrentWater < 52f && ObjectOf<FarmFertilizerStorage>().CurrentFertilizer == 0f,
                    "Reset restores the initial water and fertilizer");
                Require(!ObjectOf<FarmPlot>().IsFertilized && ObjectOf<FarmPlot>().CurrentCrop == null, "Reset clears saved crops");
                RunCoreChecks();
                Finish(0);
                return;
            }
            started = EditorApplication.timeSinceStartup;
        }
        catch (Exception error)
        {
            results.AppendLine("FAIL: " + error);
            Finish(1);
        }
    }

    private static void CheckReset()
    {
        Require(!PlayerPrefs.HasKey(FarmSaveManager.KeyPrefix + ".Farm.HasSave") && !PlayerPrefs.HasKey(FarmSaveManager.KeyPrefix + ".Farm.Coins"),
            "Confirmed reset deletes only this profile's progress");
        Require(Mathf.Abs(PlayerPrefs.GetFloat(FarmSaveManager.KeyPrefix + ".MusicVolume") - 0.31f) < 0.001f &&
            Mathf.Abs(PlayerPrefs.GetFloat(FarmSaveManager.KeyPrefix + ".EffectsVolume") - 0.59f) < 0.001f, "Reset preserves audio settings");
        Require(PlayerPrefs.GetInt(FarmSaveManager.KeyPrefix + "_Other.Coins") == 901, "Reset preserves another namespace");
    }

    private static void RunCoreChecks()
    {
        FarmCropGroupChecks.Run();
        Require(true, "Crop values, grouped models, soil alignment and camera framing pass");
        foreach (string name in new[] { "CheckCrops", "CheckRegeneration", "CheckMarket", "CheckExpansion", "CheckMissions" })
        {
            typeof(FarmSystemChecks).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            Require(true, "Core regression passes: " + name);
        }
    }

    private static void BeginBarnRoute(float ground)
    {
        Vector3[] options = {
            new Vector3(barnBounds.center.x, ground, barnBounds.min.z - 0.8f),
            new Vector3(barnBounds.center.x, ground, barnBounds.max.z + 0.8f),
            new Vector3(barnBounds.min.x - 0.8f, ground, barnBounds.center.z),
            new Vector3(barnBounds.max.x + 0.8f, ground, barnBounds.center.z) };
        var candidates = options.Select(point => NavMesh.SamplePosition(point, out var hit, 1f, NavMesh.AllAreas) ? hit.position : new Vector3(float.NaN, 0f, 0f))
            .Where(point => !float.IsNaN(point.x) && !InsideBarn(point)).ToArray();
        foreach (var first in candidates)
        foreach (var second in candidates)
        {
            if (!InsideBarn(Vector3.Lerp(first, second, 0.5f))) continue;
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(first, second, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            Require(player.Agent.Warp(first), "Player can reach a point beside the barn");
            player.Agent.isStopped = false;
            Require(player.Agent.SetDestination(second), "A complete path exists around the barn");
            goal = second;
            return;
        }
        throw new Exception("Could not find a complete route around the barn.");
    }

    private static bool InsideBarn(Vector3 point) => point.x > barnBounds.min.x + 0.05f && point.x < barnBounds.max.x - 0.05f &&
        point.z > barnBounds.min.z + 0.05f && point.z < barnBounds.max.z - 0.05f;

    private static void SeedVisualComparison()
    {
        PlayerPrefs.SetInt(FarmSaveManager.KeyPrefix + ".Farm.PurchasedPlots", 3);
        ObjectOf<FarmPlotExpansion>().LoadProgress(FarmSaveManager.KeyPrefix + ".Farm");
        var plots = UnityEngine.Object.FindObjectsByType<FarmPlot>(FindObjectsSortMode.None).OrderBy(plot => plot.transform.position.z).ToArray();
        string[] names = { "Carrot", "Corn", "Lettuce", "Wheat" };
        for (int index = 0; index < plots.Length; index++)
        {
            var crop = AssetDatabase.LoadAssetAtPath<CropData>("Assets/_Project/Crops/" + names[index] + ".asset");
            if (plots[index].Machine.CurrentState == null) plots[index].Machine.ChangeState(plots[index].Empty);
            player.Inventory.AddSeeds(crop, 1);
            bool planted = plots[index].TryPlant(crop, player.Inventory, out string message);
            Require(planted, "Prepare visual comparison: " + names[index] + " | " + message);
            plots[index].Machine.ChangeState(plots[index].Grown);
        }
        ObjectOf<FarmWallet>().AddCoins(10000);
        ObjectOf<FarmFertilizerStorage>().AddFertilizer(100f);
        ObjectOf<FarmTechnologyManager>().Unlock(AssetDatabase.LoadAssetAtPath<TechnologyData>("Assets/_Project/Tech/FarmUpgrades/Worm Composter.asset"));
    }

    private static bool CaptureNextPanel()
    {
        var canvas = ObjectOf<FarmHUD>().transform;
        string[] views = { "Map", "FirstCommunity", "SecondCommunity", "ThirdCommunity", "Market", "Upgrades", "Context", "Pause", "PauseSettings" };
        if (pendingPanel)
        {
            CheckUIInput(canvas);
            Capture(views[screen - 1]);
            pendingPanel = false;
            if (screen == views.Length) ClosePanels(canvas);
            started = EditorApplication.timeSinceStartup;
            return true;
        }
        if (screen >= views.Length) return false;
        ClosePanels(canvas);
        string view = views[screen++];
        if (view == "Map") Click(canvas, "MapTab");
        else if (view.EndsWith("Community")) { Click(canvas, "MapTab"); Click(canvas, view + "Button"); }
        else if (view == "Market") { Click(canvas, "MapTab"); Click(canvas, "MarketMapButton"); }
        else if (view == "Upgrades") Click(canvas, "TechnologiesTab");
        else if (view == "Context") ObjectOf<FarmPlotContextMenu>().Show(ObjectOf<FarmPlot>(), new Vector2(750f, 450f));
        else if (view == "Pause") ObjectOf<FarmUIController>().PauseGame();
        else ObjectOf<FarmUIController>().OpenOptions();
        ObjectOf<FarmHUD>().RefreshHUD();
        pendingPanel = true;
        started = EditorApplication.timeSinceStartup;
        return true;
    }

    private static void CheckUIInput(Transform canvas)
    {
        foreach (var dropdown in canvas.GetComponentsInChildren<TMP_Dropdown>(false))
        {
            if (!dropdown.interactable || dropdown.options.Count == 0) continue;
            dropdown.Show();
            Canvas.ForceUpdateCanvases();
            FarmSystemChecks.Capture("Dropdown_" + dropdown.name);
            Canvas.ForceUpdateCanvases();
            var items = dropdown.GetComponentsInChildren<Toggle>(false);
            Require(items.Length > 0, "Dropdown creates selectable options: " + dropdown.name);
            GameObject hit = null;
            foreach (var item in items)
            {
                var candidate = Raycast(item.targetGraphic);
                if (candidate != null && candidate.transform.IsChildOf(item.transform)) { hit = candidate; break; }
            }
            Require(hit != null, "Visible dropdown option receives pointer input: " + dropdown.name);
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerClickHandler);
            dropdown.Hide();
        }
        foreach (var slider in canvas.GetComponentsInChildren<Slider>(false))
        {
            if (!slider.interactable || slider.targetGraphic == null) continue;
            var hit = Raycast(slider.targetGraphic);
            Require(hit != null && hit.GetComponentInParent<Slider>() == slider, "Slider receives pointer input: " + slider.name);
        }
    }

    private static GameObject Raycast(Graphic graphic)
    {
        var canvas = graphic.canvas;
        var rect = graphic.rectTransform;
        var pointer = new PointerEventData(EventSystem.current) {
            position = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                rect.TransformPoint(rect.rect.center)) };
        var hits = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        return hits.Count > 0 ? hits[0].gameObject : null;
    }

    private static void ClosePanels(Transform canvas)
    {
        var menus = ObjectOf<FarmUIController>();
        if (menus.IsPaused) menus.ResumeGame();
        ObjectOf<FarmPlotContextMenu>().Close();
        foreach (string name in new[] { "ScreenOverlay", "MapPanel", "ShopPanel", "TechnologiesPanel", "FirstCommunityPanel", "SecondCommunityPanel", "ThirdCommunityPanel" })
        { var panel = Find(canvas, name); if (panel != null) panel.gameObject.SetActive(false); }
    }

    private static void CheckHud()
    {
        var canvas = ObjectOf<FarmHUD>().transform;
        foreach (string name in new[] { "CoinsHudCard", "WaterHudCard", "FertilizerHudCard" }) Require(Find(canvas, name) != null, "Readable HUD card exists: " + name);
        Require(Read<TMP_Text>(ObjectOf<FarmHUD>(), "coinCounterText").text.Contains("10,050"), "Coin count uses English thousands formatting");
        Require(Read<TMP_Text>(ObjectOf<FarmHUD>(), "waterAmountText").text.Contains(" / "), "Water count is compact and separate from its label");
    }

    private static void Capture(string name)
    {
        Canvas.ForceUpdateCanvases();
        FarmSystemChecks.Capture(name);
        var canvas = (GameObject.Find("Main Canvas") ?? GameObject.Find("MainMenuCanvas")).transform;
        foreach (var text in canvas.GetComponentsInChildren<TMP_Text>(false))
        {
            text.ForceMeshUpdate();
            if (text.enabled && text.color.a > 0.1f && text.isTextOverflowing) overflow.AppendLine(name + ": " + text.name + " | " + text.text.Replace('\n', ' '));
        }
    }

    private static void Click(Transform root, string name) => Find(root, name).GetComponent<Button>().onClick.Invoke();
    private static Transform Find(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == name);
    private static T ObjectOf<T>() where T : UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>();
    private static T Read<T>(object target, string field) => (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); results.AppendLine("PASS: " + message); }

    private static void Finish(int code)
    {
        FarmSaveChecks.CleanupTestKeys();
        PlayerPrefs.DeleteKey(FarmSaveManager.KeyPrefix + "_Other.Coins");
        PlayerPrefs.Save();
        AudioListener.volume = 1f;
        Directory.CreateDirectory(Report);
        File.WriteAllText(Report + "PlayModeChecks.txt", results + "Failures: " + code + "\n");
        File.WriteAllText(Report + "UiOverflow.txt", overflow.ToString());
        SessionState.SetBool(Session, false);
        SessionState.SetInt("FarmDeliveryExit", code);
        EditorApplication.update -= Tick;
        EditorApplication.update += Exit;
        EditorApplication.isPlaying = false;
    }

    private static void Exit()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        EditorApplication.Exit(SessionState.GetInt("FarmDeliveryExit", 1));
    }
}
