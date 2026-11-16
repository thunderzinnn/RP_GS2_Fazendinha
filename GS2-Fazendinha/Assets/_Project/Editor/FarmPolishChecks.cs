using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FarmPolishChecks
{
    private const string Session = "FarmPolishChecks";
    private const string Report = "../../Verification/FarmPolish/";
    [Serializable] private class VisualRecord { public string mesh; public Vector3 center; public Vector3 size; }
    [Serializable] private class Composition { public VisualRecord[] scenery; public VisualRecord[] plots; }
    private static readonly StringBuilder results = new StringBuilder();
    private static int phase;
    private static int harvests;
    private static double started;
    private static FarmPlot plot;
    private static CropData crop;
    private static PlayerFarmController player;
    private static ParticleSystem[] previousEffects;
    private static AudioClip previousHarvest;
    private static int snapshotFrame;
    private static readonly System.Collections.Generic.HashSet<AudioClip> heardHarvests = new System.Collections.Generic.HashSet<AudioClip>();

    public static void Run()
    {
        SessionState.SetBool(Session, true);
        SessionState.SetString("FarmPolishPrefix", "GoldenRootTest_Polish_" + Guid.NewGuid().ToString("N"));
        SessionState.SetString("FarmSystemAuditReport", Report);
        ConfigureProfile();
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainLevel.unity");
        var source = new Composition();
        source.scenery = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(IsScenery).Select(Record).ToArray();
        source.plots = UnityEngine.Object.FindObjectsByType<FarmPlot>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Select(item => Record(item.transform.Find("SoilMesh").GetComponentInChildren<Renderer>())).ToArray();
        SessionState.SetString("FarmPolishComposition", JsonUtility.ToJson(source));
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainMenu.unity");
        EditorApplication.isPlaying = true;
    }

    private static bool IsScenery(Renderer renderer) => renderer.GetComponentInParent<Canvas>(true) == null &&
        renderer.GetComponentInParent<FarmPlot>(true) == null && renderer.GetComponentInParent<PlayerFarmController>(true) == null;

    private static VisualRecord Record(Renderer renderer)
    {
        var mesh = renderer.GetComponent<MeshFilter>();
        return new VisualRecord { mesh = mesh != null && mesh.sharedMesh != null ? mesh.sharedMesh.name : renderer.name,
            center = renderer.bounds.center, size = renderer.bounds.size };
    }

    [InitializeOnLoadMethod]
    private static void Restore()
    {
        if (!SessionState.GetBool(Session, false)) return;
        ConfigureProfile();
        AudioListener.volume = 0f;
        phase = 0;
        harvests = 0;
        started = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
    }

    private static void ConfigureProfile()
    {
        FarmSaveManager.KeyPrefix = SessionState.GetString("FarmPolishPrefix", "GoldenRootTest_Polish");
        FarmSaveManager.AutomaticSavingEnabled = false;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            double elapsed = EditorApplication.timeSinceStartup - started;
            if ((phase == 0 || phase == 1 || phase == 2 || phase == 7) && elapsed < 1d) return;
            if (phase >= 3 && phase <= 6 && elapsed > 20d) throw new Exception("Timed out waiting for player action phase " + phase);
            if (phase == 0)
            {
                var backdrop = UnityEngine.Object.FindFirstObjectByType<MenuFarmBackdrop>();
                var expected = JsonUtility.FromJson<Composition>(SessionState.GetString("FarmPolishComposition", ""));
                var actualScenery = backdrop.transform.Find("MainLevelScenery").GetComponentsInChildren<Renderer>(true).Select(Record).ToArray();
                Require(expected.scenery.Length == actualScenery.Length, "Menu copies every MainLevel scenery renderer, including the composter");
                foreach (var record in expected.scenery) Require(actualScenery.Any(other => Matches(record, other)),
                    "Scenery mesh keeps world position and size: " + record.mesh);
                var slots = Read<MenuFarmBackdrop.PlotVisual[]>(backdrop, "plots");
                Require(slots.Length == 4, "All four decorative plots are unlocked");
                var actualPlots = slots.Select(slot => Record(slot.cropAnchor.parent.Find("PlotVisual").GetComponentInChildren<Renderer>())).ToArray();
                foreach (var record in expected.plots) Require(actualPlots.Any(other => Matches(record, other)), "Plot keeps the MainLevel composition: " + record.center);
                Require(UnityEngine.Object.FindFirstObjectByType<FarmPlot>() == null && !FarmSaveManager.Instance.CanSaveProgress,
                    "Decorative farm remains independent from gameplay and saves");
                foreach (string field in new[] { "waterEffect", "fertilizerEffect", "harvestEffect" })
                {
                    var effect = Read<Transform>(backdrop, field).GetComponent<ParticleSystem>();
                    Require(effect != null && effect.main.loop, "Menu uses reusable looping prefab: " + field);
                }
                for (int step = 0; step < 1600; step++) backdrop.TickSequence(0.1f);
                Invoke(backdrop, "UpdateActionEffects");
                FarmSystemChecks.Capture("MainMenuMatched");
                phase = 1;
                started = EditorApplication.timeSinceStartup;
            }
            else if (phase == 1)
            {
                SceneManager.LoadScene("MainLevel");
                phase = 2;
                started = EditorApplication.timeSinceStartup;
            }
            else if (phase == 2)
            {
                player = UnityEngine.Object.FindFirstObjectByType<PlayerFarmController>();
                plot = UnityEngine.Object.FindObjectsByType<FarmPlot>(FindObjectsSortMode.None).First(item => item.gameObject.activeInHierarchy);
                crop = AssetDatabase.LoadAssetAtPath<CropData>("Assets/_Project/Crops/Carrot.asset");
                player.Inventory.AddSeeds(crop, 16);
                foreach (var item in UnityEngine.Object.FindObjectsByType<FarmPlot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    Require(Read<GameObject>(item, "wateringEffectPrefab") != null && Read<GameObject>(item, "fertilizerEffectPrefab") != null,
                        "Real plot has both effect prefabs: " + item.name);
                foreach (var item in UnityEngine.Object.FindObjectsByType<FarmPlot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    Require(Read<GameObject>(item, "harvestEffectPrefab") != null, "Real plot has harvest particles: " + item.name);
                BeginPlanting();
            }
            else if (phase == 3 && plot.CurrentCrop != null)
            {
                PlayerPrefs.SetFloat(FarmSaveManager.KeyPrefix + ".Farm.Water", 100f);
                player.Tank.LoadProgress(FarmSaveManager.KeyPrefix + ".Farm");
                Request(FarmTool.Water);
                phase = 4;
            }
            else if (phase == 4 && plot.Hydration > crop.initialHydration + 2f)
            {
                var water = NewEffect();
                Require(water != null && water.name.StartsWith("WaterDroplets"), "Walking and watering spawns the droplets prefab");
                Require(Read<AudioClip[]>(AudioManager.Instance, "wateringSounds").Contains(AudioManager.Instance.LastPlayedSound), "Real watering calls the randomized sound group");
                water.Simulate(0.25f, true, false); water.Play();
                CheckNativeMotion(water, false);
                if (harvests == 0) { snapshotFrame = Time.frameCount; phase = 40; }
                else BeginFertilizing();
            }
            else if (phase == 40 && Time.frameCount > snapshotFrame + 2)
            {
                FarmSystemChecks.Capture("WateringEffect");
                BeginFertilizing();
            }
            else if (phase == 5 && plot.IsFertilized)
            {
                var fertilizer = NewEffect();
                Require(fertilizer != null && fertilizer.name.StartsWith("FertilizerBoost"), "Walking and fertilizing spawns rising plus symbols");
                Require(fertilizer.GetComponent<ParticleSystemRenderer>().mesh != null && fertilizer.GetComponents<MonoBehaviour>().Length == 0,
                    "Fertilizer uses native mesh particles without an animation script");
                fertilizer.Simulate(0.3f, true, false); fertilizer.Play();
                CheckNativeMotion(fertilizer, true);
                int count = UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Length;
                Require(!plot.TryFertilize(out _) && UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Length == count,
                    "Invalid duplicate fertilizer does not spawn effects");
                Require(!plot.TryWater(null, out _) && UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Length == count,
                    "Failed watering does not spawn effects");
                if (harvests == 0) { snapshotFrame = Time.frameCount; phase = 50; }
                else BeginHarvesting();
            }
            else if (phase == 50 && Time.frameCount > snapshotFrame + 2)
            {
                FarmSystemChecks.Capture("FertilizerEffect");
                BeginHarvesting();
            }
            else if (phase == 6 && plot.CurrentCrop == null)
            {
                var effect = NewEffect();
                Require(effect != null && effect.name.StartsWith("HarvestSparkles"), "Real harvest spawns native sparkles");
                CheckNativeMotion(effect, true);
                var sound = AudioManager.Instance.LastPlayedSound;
                Require(Read<AudioClip[]>(AudioManager.Instance, "harvestSounds").Contains(sound), "Real harvest chooses one of the three harvest sounds");
                Require(previousHarvest == null || previousHarvest != sound, "Consecutive real harvests do not repeat the same sound");
                previousHarvest = sound;
                heardHarvests.Add(sound);
                harvests++;
                if (harvests == 1) { snapshotFrame = Time.frameCount; phase = 60; }
                else ContinueHarvests();
            }
            else if (phase == 60 && Time.frameCount > snapshotFrame + 2)
            {
                FarmSystemChecks.Capture("HarvestEffect");
                ContinueHarvests();
            }
            else if (phase == 7)
            {
                Require(UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Length == 0,
                    "One-shot gameplay effects remove themselves after finishing");
                SceneManager.LoadScene("Tutorial");
                phase = 8;
                started = EditorApplication.timeSinceStartup;
            }
            else if (phase == 8 && elapsed >= 1d)
            {
                var tutorialPlot = UnityEngine.Object.FindFirstObjectByType<FarmPlot>();
                Require(Read<GameObject>(tutorialPlot, "wateringEffectPrefab") != null && Read<GameObject>(tutorialPlot, "fertilizerEffectPrefab") != null,
                    "Tutorial plot also has both effect prefabs");
                CheckTutorialEffects(tutorialPlot);
                snapshotFrame = Time.frameCount;
                phase = 9;
            }
            else if (phase == 9 && Time.frameCount > snapshotFrame + 2)
            {
                FarmSystemChecks.Capture("TutorialActionEffects");
                var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/FarmPlot.prefab").GetComponent<FarmPlot>();
                Require(Read<GameObject>(source, "wateringEffectPrefab") != null && Read<GameObject>(source, "fertilizerEffectPrefab") != null && Read<GameObject>(source, "harvestEffectPrefab") != null,
                    "Base FarmPlot prefab retains all three effects for future instances");
                Finish(0);
            }
        }
        catch (Exception error)
        {
            results.AppendLine("FAIL: " + error);
            Finish(1);
        }
    }

    private static void BeginPlanting()
    {
        Request(FarmTool.Plant);
        phase = 3;
    }

    private static void ContinueHarvests()
    {
        if (harvests < 12) { BeginPlanting(); return; }
        Require(Read<AudioClip[]>(AudioManager.Instance, "harvestSounds").Where(clip => clip != null).Distinct().Count() == 3,
            "Three distinct harvest sounds are configured for real interactions");
        phase = 7;
        started = EditorApplication.timeSinceStartup + 1.5d;
    }

    private static void CheckTutorialEffects(FarmPlot target)
    {
        var inventory = UnityEngine.Object.FindFirstObjectByType<FarmInventory>();
        var tank = UnityEngine.Object.FindFirstObjectByType<FarmWaterTank>();
        var fertilizer = UnityEngine.Object.FindFirstObjectByType<FarmFertilizerStorage>();
        var carrot = AssetDatabase.LoadAssetAtPath<CropData>("Assets/_Project/Crops/TutorialCarrot.asset");
        inventory.AddSeeds(carrot, 1);
        Require(target.TryPlant(carrot, inventory, out _), "Tutorial planting succeeds");
        previousEffects = UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
        Require(target.TryWater(tank, out _) && NewEffect() != null, "Tutorial watering spawns native particles");
        CheckNativeMotion(NewEffect(), false);
        fertilizer.AddFertilizer(100f);
        previousEffects = UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
        Require(target.TryFertilize(out _) && NewEffect() != null, "Tutorial fertilizing spawns native particles");
        CheckNativeMotion(NewEffect(), true);
        target.Machine.ChangeState(target.Grown);
        previousEffects = UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
        Require(target.TryHarvest(inventory, out _) && NewEffect() != null, "Tutorial harvest spawns native particles");
        CheckNativeMotion(NewEffect(), true);
    }

    private static void BeginFertilizing()
    {
        UnityEngine.Object.FindFirstObjectByType<FarmFertilizerStorage>().AddFertilizer(100f);
        Request(FarmTool.Fertilize);
        phase = 5;
    }

    private static void CheckNativeMotion(ParticleSystem effect, bool rising)
    {
        Require(effect.main.stopAction == ParticleSystemStopAction.Destroy && !effect.main.loop,
            "Gameplay particles destroy themselves without a cleanup script");
        effect.Simulate(0.15f, true, true);
        var parts = new ParticleSystem.Particle[32];
        int count = effect.GetParticles(parts);
        Require(count > 0, "Native emission produces visible particles");
        float before = parts[0].position.y;
        effect.Simulate(0.2f, true, false);
        Require(effect.GetParticles(parts) == count && (rising ? parts[0].position.y > before : parts[0].position.y < before),
            rising ? "Native velocity makes fertilizer symbols rise" : "Native velocity makes water drops fall");
        effect.Play();
    }

    private static void BeginHarvesting()
    {
        plot.Machine.ChangeState(plot.Grown);
        Request(FarmTool.Harvest);
        phase = 6;
    }

    private static void Request(FarmTool tool)
    {
        previousEffects = UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
        Require(player.RequestAction(plot, tool, crop), "Player can queue action: " + tool);
        started = EditorApplication.timeSinceStartup;
    }

    private static ParticleSystem NewEffect() => UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)
        .FirstOrDefault(effect => !previousEffects.Contains(effect));
    private static bool Matches(VisualRecord first, VisualRecord second) => first.mesh == second.mesh &&
        Vector3.Distance(first.center, second.center) < 0.01f && Vector3.Distance(first.size, second.size) < 0.01f;
    private static T Read<T>(object target, string field) => (T)target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
    private static void Invoke(object target, string method) => target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);
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
        SessionState.SetInt("FarmPolishExit", code);
        EditorApplication.update -= Tick;
        EditorApplication.update += Exit;
        EditorApplication.isPlaying = false;
    }

    private static void Exit()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        EditorApplication.Exit(SessionState.GetInt("FarmPolishExit", 1));
    }
}
