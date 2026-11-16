using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MenuFarmBackdropChecks
{
    private const string Session = "MenuFarmBackdropChecks";
    private const string Report = "../../Verification/MenuBackdrop/";
    private static readonly StringBuilder results = new StringBuilder();
    private static int phase;
    private static double started;
    private static MenuFarmBackdrop backdrop;
    private static AudioManager audio;

    public static void Run()
    {
        SessionState.SetBool(Session, true);
        SessionState.SetString("MenuBackdropPrefix", "GoldenRootTest_Menu_" + Guid.NewGuid().ToString("N"));
        SessionState.SetString("FarmSystemAuditReport", Report);
        ConfigureProfile();
        PlayerPrefs.SetInt(FarmSaveManager.KeyPrefix + ".Farm.Coins", 321);
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
        phase = 0;
        started = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
    }

    private static void ConfigureProfile()
    {
        FarmSaveManager.KeyPrefix = SessionState.GetString("MenuBackdropPrefix", "GoldenRootTest_Menu");
        FarmSaveManager.AutomaticSavingEnabled = false;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.timeSinceStartup - started < 1d) return;
        try
        {
            if (phase == 0)
            {
                backdrop = UnityEngine.Object.FindFirstObjectByType<MenuFarmBackdrop>();
                Require(backdrop != null, "Decorative farm exists in MainMenu");
                Require(Camera.main.GetComponent<CinemachineBrain>() != null, "Menu camera uses Cinemachine Brain");
                var virtualCamera = backdrop.GetComponentInChildren<CinemachineCamera>();
                Require(virtualCamera != null && virtualCamera.Follow != null && virtualCamera.LookAt != null,
                    "Cinemachine camera tracks the decorative farm target");
                Require(Camera.main.transform.position.sqrMagnitude > 100f, "Cinemachine places the output camera in its orbit");
                Require(UnityEngine.Object.FindFirstObjectByType<FarmInventory>() == null &&
                    UnityEngine.Object.FindFirstObjectByType<FarmMissionBoard>() == null &&
                    UnityEngine.Object.FindFirstObjectByType<PlayerFarmController>() == null &&
                    UnityEngine.Object.FindFirstObjectByType<FarmPlot>() == null, "Backdrop contains no gameplay inventory, missions, player controller or farm plots");
                Require(!FarmSaveManager.Instance.CanSaveProgress, "Menu cannot save farm resources");
                var plots = Read<MenuFarmBackdrop.PlotVisual[]>(backdrop, "plots");
                Require(plots.Length == 4 && plots.All(plot => plot.plantedModel != null && plot.growingModel != null && plot.grownModel != null),
                    "Four plots have all three replaceable visual stages");
                var actor = Read<Transform>(backdrop, "farmer");
                Require(Read<Transform>(backdrop, "farmerVisual") != null && actor.GetComponent<UnityEngine.AI.NavMeshAgent>() == null,
                    "Farmer visual is replaceable and independent from gameplay navigation");
                var visited = new System.Collections.Generic.HashSet<string>();
                var phases = new System.Collections.Generic.HashSet<string>();
                var stageSeen = new System.Collections.Generic.HashSet<string>();
                for (int step = 0; step < 1200; step++)
                {
                    backdrop.TickSequence(0.1f);
                    phases.Add(Read<object>(backdrop, "phase").ToString());
                    foreach (var plot in plots)
                    {
                        if (Vector3.Distance(actor.position, plot.workPoint.position) < 0.1f) visited.Add(plot.workPoint.parent.name);
                        if (plot.plantedModel.activeSelf) stageSeen.Add("Planted");
                        if (plot.growingModel.activeSelf) stageSeen.Add("Growing");
                        if (plot.grownModel.activeSelf) stageSeen.Add("Grown");
                    }
                }
                Require(visited.Count == 4, "Farmer visits all four plots across repeated cycles");
                Require(phases.Count == 5, "Sequence includes walking, planting, watering, fertilizing and harvesting");
                Require(stageSeen.Count == 3, "Crops show planted, growing and grown stages");
                Require(PlayerPrefs.GetInt(FarmSaveManager.KeyPrefix + ".Farm.Coins") == 321 &&
                    !PlayerPrefs.HasKey(FarmSaveManager.KeyPrefix + ".Farm.HasSave"), "Repeated background cycles do not change or create farm saves");
                audio = AudioManager.Instance;
                Require(audio != null && Read<AudioSource>(audio, "musicSource").clip.name.StartsWith("MSC_Farm_MainMenu"), "Menu playlist continues with the backdrop");
                Require(audio.LastPlayedSound == null, "Decorative actions do not emit gameplay effects over menu music");
                var orbit = Read<CinemachineOrbitalFollow>(backdrop, "cameraOrbit");
                SessionState.SetFloat("MenuOrbitBefore", orbit.HorizontalAxis.Value);
                FarmSystemChecks.Capture("MenuFront");
                phase = 1;
            }
            else if (phase == 1)
            {
                var orbit = Read<CinemachineOrbitalFollow>(backdrop, "cameraOrbit");
                Require(Mathf.Abs(Mathf.DeltaAngle(SessionState.GetFloat("MenuOrbitBefore", 0f), orbit.HorizontalAxis.Value)) > 1f,
                    "Camera orbit advances automatically over time");
                typeof(MenuFarmBackdrop).GetField("orbitDegreesPerSecond", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(backdrop, 0f);
                orbit.HorizontalAxis.Value = 70f;
                phase = 2;
            }
            else if (phase == 2)
            {
                FarmSystemChecks.Capture("MenuSide");
                Read<CinemachineOrbitalFollow>(backdrop, "cameraOrbit").HorizontalAxis.Value = 175f;
                phase = 3;
            }
            else if (phase == 3)
            {
                FarmSystemChecks.Capture("MenuBack");
                Read<CinemachineOrbitalFollow>(backdrop, "cameraOrbit").HorizontalAxis.Value = -28f;
                phase = 4;
            }
            else if (phase == 4)
            {
                var canvas = GameObject.Find("MainMenuCanvas").GetComponent<Canvas>();
                foreach (string name in new[] { "GameTitle", "PlayLabel" })
                {
                    var text = FarmMenuSetup.Find(canvas.transform, name).GetComponent<TMP_Text>();
                    text.ForceMeshUpdate();
                    Require(!text.isTextOverflowing, "Menu text fits its area: " + name);
                }
                var shade = FarmMenuSetup.Find(canvas.transform, "MenuBackdropShade").GetComponent<UnityEngine.UI.Image>();
                Require(!shade.raycastTarget, "Readability shade does not block buttons");
                canvas.enabled = false;
                FarmSystemChecks.Capture("FarmDetail");
                canvas.enabled = true;
                var menus = UnityEngine.Object.FindFirstObjectByType<FarmUIController>();
                menus.OpenOptions();
                FarmSystemChecks.Capture("MenuOptions");
                menus.CloseOptions();
                menus.PlayGame();
                phase = 5;
            }
            else if (phase == 5)
            {
                Require(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Tutorial", "Play remains connected to Tutorial");
                Require(UnityEngine.Object.FindFirstObjectByType<MenuFarmBackdrop>() == null && Camera.main.GetComponent<CinemachineBrain>() == null,
                    "Backdrop and menu camera do not enter gameplay scenes");
                Require(AudioManager.Instance == audio && Read<AudioSource>(audio, "musicSource").clip.name.StartsWith("MSC_Farm_Gameplay"),
                    "Audio singleton survives menu-to-game transition");
                Require(PlayerPrefs.GetInt(FarmSaveManager.KeyPrefix + ".Farm.Coins") == 321, "Farm save remains unchanged after the transition");
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

    private static T Read<T>(object target, string field) => (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        results.AppendLine("PASS: " + message);
    }

    private static void Finish(int code)
    {
        FarmSaveChecks.CleanupTestKeys();
        AudioListener.volume = 1f;
        Directory.CreateDirectory(Report);
        File.WriteAllText(Report + "PlayModeChecks.txt", results + "Failures: " + code + "\n");
        SessionState.SetBool(Session, false);
        SessionState.SetInt("MenuBackdropExit", code);
        EditorApplication.update -= Tick;
        EditorApplication.update += Exit;
        EditorApplication.isPlaying = false;
    }

    private static void Exit()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        EditorApplication.Exit(SessionState.GetInt("MenuBackdropExit", 1));
    }
}
