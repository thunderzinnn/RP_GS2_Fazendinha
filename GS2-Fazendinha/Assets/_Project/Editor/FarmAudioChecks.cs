using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Audio;

public static class FarmAudioChecks
{
    private const string Session = "FarmAudioChecks";
    private const string Report = "../../Verification/Audio/";
    private static readonly StringBuilder results = new StringBuilder();
    private static int phase;
    private static double started;
    private static AudioManager original;
    private static AudioClip shortTrack;
    private static AudioClip longTrack;
    private static AudioClip retainedTrack;
    private static float retainedTime;

    public static void Run()
    {
        SessionState.SetBool(Session, true);
        SessionState.SetString("FarmAudioPrefix", "GoldenRootTest_Audio_" + Guid.NewGuid().ToString("N"));
        ConfigureProfile();
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainMenu.unity");
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    private static void Restore()
    {
        if (!SessionState.GetBool(Session, false)) return;
        ConfigureProfile();
        phase = 0;
        started = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
    }

    private static void ConfigureProfile()
    {
        FarmSaveManager.KeyPrefix = SessionState.GetString("FarmAudioPrefix", "GoldenRootTest_Audio");
        FarmSaveManager.AutomaticSavingEnabled = false;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        if (EditorApplication.timeSinceStartup - started < (phase == 1 ? 0.7d : 1d)) return;
        AudioListener.volume = 0f;
        try
        {
            if (phase == 0)
            {
                original = AudioManager.Instance;
                Require(original != null, "Audio singleton initialized from MainMenu");
                Require(original.GetComponents<AudioSource>().Length == 3, "Exactly three sources: music, effects and ambience");
                Require(!PrefabUtility.IsPartOfPrefabInstance(original.gameObject), "AudioManager is a scene object, not a prefab instance");
                CheckMixer();
                var music = Read<AudioSource>(original, "musicSource");
                Require(music.isPlaying && !music.loop && music.clip.name == "MSC_Farm_MainMenu_01", "Menu starts first playlist track without single-track looping");
                Require(Read<AudioSource>(original, "ambienceSource").clip == null, "Menu has no farm ambience");
                foreach (string field in new[] { "menuPlaylist", "gameplayPlaylist" })
                {
                    var clips = Read<AudioClip[]>(original, field);
                    Require(clips.Length == 4 && clips.All(clip => clip != null), field + " has four valid tracks");
                    for (int index = 0; index < clips.Length; index++) Require(clips[index].name.EndsWith("0" + (index + 1)), field + " order " + index);
                }
                CheckSound(original.PlayUIClick, "SFX_UI_Click_01");
                CheckSound(original.PlayFarmClick, "SFX_UI_Click_02");
                CheckSound(original.PlayCropClick, "SFX_UI_Click_03");
                CheckSound(original.PlayFarmClose, "SFX_UI_Close_01");
                CheckSound(original.PlayMenuClose, "SFX_UI_Close_02");
                CheckSound(() => original.PlayTechnologyPurchase(true), "SFX_UI_TechAdquired");
                CheckSound(() => original.PlayTechnologyPurchase(false), "SFX_UI_TechAdquired_01");
                CheckRandom(original.PlayWatering, "wateringSounds", 4);
                CheckRandom(original.PlayHarvest, "harvestSounds", 3);
                CheckRandom(original.PlayFertilizer, "fertilizerSounds", 3);
                Read<AudioSource>(original, "effectsSource").Stop();
                original.StopMusic();
                Invoke(original, "UpdatePlaylist");
                Require(!music.isPlaying, "StopMusic does not restart the playlist");
                shortTrack = AudioClip.Create("Short playlist verification", 2000, 1, 8000, false);
                longTrack = AudioClip.Create("Long playlist verification", 240000, 1, 8000, false);
                Write(original, "currentPlaylist", new[] { shortTrack, longTrack });
                Write(original, "playlistIndex", -1);
                Write(original, "playlistActive", true);
                Invoke(original, "PlayNextTrack");
                Require(music.clip == shortTrack && music.isPlaying, "Short verification track starts");
                phase = 1;
            }
            else if (phase == 1)
            {
                var music = Read<AudioSource>(original, "musicSource");
                if ((music.clip != longTrack || !music.isPlaying) && EditorApplication.timeSinceStartup - started < 5d) return;
                Require(music.clip == longTrack && music.isPlaying, "Natural track ending advances to next track; current=" +
                    (music.clip != null ? music.clip.name : "none") + ", playing=" + music.isPlaying + ", time=" + music.time);
                var tracks = Read<AudioClip[]>(original, "menuPlaylist");
                Write(original, "currentPlaylist", tracks);
                Write(original, "playlistIndex", -1);
                for (int index = 0; index < 5; index++)
                {
                    music.Stop();
                    Invoke(original, "UpdatePlaylist");
                    Require(music.clip == tracks[index % tracks.Length], "Playlist sequence including wrap-around: " + index);
                }
                Write(original, "currentPlaylist", new[] { (AudioClip)null, tracks[0], null });
                Write(original, "playlistIndex", -1);
                Invoke(original, "PlayNextTrack");
                Require(music.clip == tracks[0], "Null entries are skipped");
                Write(original, "sceneConfigured", false);
                Invoke(original, "ConfigureSceneAudio", "MainMenu");
                original.SetMusicVolume(0.23f);
                original.SetEffectsVolume(0.41f);
                FarmSaveManager.SaveAudioSettings();
                PlayerPrefs.Save();
                UnityEngine.Object.FindFirstObjectByType<FarmUIController>().PlayGame();
                phase = 2;
            }
            else if (phase == 2)
            {
                Require(SceneManager.GetActiveScene().name == "Tutorial", "Play enters Tutorial");
                CheckPersistentAudio();
                var music = Read<AudioSource>(original, "musicSource");
                var ambience = Read<AudioSource>(original, "ambienceSource");
                Require(music.clip.name == "MSC_Farm_Gameplay_01" && music.isPlaying, "Tutorial starts gameplay playlist");
                Require(ambience.clip.name == "AMB_Farm_Background_01" && ambience.isPlaying && ambience.loop, "Tutorial ambience loops on its own source");
                CheckSound(original.PlayMissionComplete, "SFX_UI_CompleteMission_02");
                CheckFarmActions();
                retainedTrack = music.clip;
                retainedTime = music.time;
                var tutorial = UnityEngine.Object.FindFirstObjectByType<FarmTutorialController>();
                typeof(FarmTutorialController).GetProperty("CurrentStep").SetValue(tutorial, FarmTutorialStep.Completed);
                tutorial.ContinueToGame();
                Require(original.LastPlayedSound.name == "SFX_Gameplay_CompleteTutorial_01", "Start Farming plays tutorial completion sound");
                phase = 3;
            }
            else if (phase == 3)
            {
                Require(SceneManager.GetActiveScene().name == "MainLevel", "Tutorial enters MainLevel");
                CheckPersistentAudio();
                var music = Read<AudioSource>(original, "musicSource");
                Require(music.clip == retainedTrack && music.time >= retainedTime, "Tutorial to farm keeps the current music and playback position");
                var ambience = Read<AudioSource>(original, "ambienceSource");
                Require(ambience.clip.name == "AMB_Farm_Background_02" && ambience.isPlaying, "Farm uses Background 02");
                var effects = Read<AudioSource>(original, "effectsSource");
                effects.Stop();
                Write(original, "chickenTimer", 25f);
                Invoke(original, "UpdateAmbience");
                Require(!effects.isPlaying && ambience.isPlaying && Read<float>(original, "chickenTimer") < 1f, "Chicken uses ambience routing without occupying the SFX source");
                CheckPurchasesAndMission();
                var hud = UnityEngine.Object.FindFirstObjectByType<FarmHUD>();
                FarmMenuSetup.Find(hud.transform, "MapTab").GetComponent<Button>().onClick.Invoke();
                Require(original.LastPlayedSound.name == "SFX_UI_Click_02", "Map button uses farm UI click");
                FarmMenuSetup.Find(hud.transform, "CloseMapButton").GetComponent<Button>().onClick.Invoke();
                Require(original.LastPlayedSound.name == "SFX_UI_Close_01", "Closing map uses farm close sound");
                var menus = UnityEngine.Object.FindFirstObjectByType<FarmUIController>();
                menus.PauseGame();
                Require(original.LastPlayedSound.name == "SFX_UI_Click_01" && music.isPlaying, "Pause keeps music and uses menu click");
                menus.ResumeGame();
                Require(original.LastPlayedSound.name == "SFX_UI_Close_02", "Resume uses menu close sound");
                SceneManager.LoadScene("MainMenu");
                phase = 4;
            }
            else if (phase == 4)
            {
                CheckPersistentAudio();
                Require(Read<AudioSource>(original, "musicSource").clip.name == "MSC_Farm_MainMenu_01", "Returning to menu restarts its own playlist");
                Require(!Read<AudioSource>(original, "ambienceSource").isPlaying, "Farm ambience stops in the menu");
                Require(Mathf.Abs(PlayerPrefs.GetFloat(FarmSaveManager.KeyPrefix + ".MusicVolume") - 0.23f) < 0.0001f,
                    "Music volume remains saved with PlayerPrefs");
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

    private static void CheckPersistentAudio()
    {
        Require(AudioManager.Instance == original && UnityEngine.Object.FindObjectsByType<AudioManager>(FindObjectsSortMode.None).Length == 1,
            "Same singleton persists without duplicated audio managers");
        CheckGain("MusicVolume", 0.23f);
        CheckGain("SFXVolume", 0.41f);
        CheckGain("AmbienceVolume", 0.123f);
    }

    private static void CheckMixer()
    {
        var mixer = Read<AudioMixer>(original, "mixer");
        Require(mixer != null && AssetDatabase.GetAssetPath(mixer) == FarmAudioSetup.MixerPath, "FarmAudio mixer is assigned");
        var visible = (Array)mixer.GetType().GetMethod("GetCurrentViewGroupList").Invoke(mixer, null);
        Require(visible.Length == 4, "Mixer window shows all four channel strips");
        foreach (string name in new[] { "Master", "Music", "SFX", "Ambience" })
            Require(mixer.FindMatchingGroups(name).Any(group => group.name == name), "Mixer group exists: " + name);
        string[] sources = { "musicSource", "effectsSource", "ambienceSource" };
        string[] groups = { "Music", "SFX", "Ambience" };
        for (int index = 0; index < sources.Length; index++)
        {
            var source = Read<AudioSource>(original, sources[index]);
            Require(source.outputAudioMixerGroup != null && source.outputAudioMixerGroup.name == groups[index], "Source routing: " + sources[index]);
            Require(Mathf.Approximately(source.volume, 1f), "Source avoids double attenuation: " + sources[index]);
        }
        float music = original.MusicVolume;
        float effects = original.EffectsVolume;
        foreach (float volume in new[] { 0f, 0.00001f, 0.25f, 0.5f, 1f })
        {
            original.SetMusicVolume(volume);
            CheckGain("MusicVolume", volume);
            original.SetEffectsVolume(volume);
            CheckGain("SFXVolume", volume);
            CheckGain("AmbienceVolume", volume * Read<float>(original, "ambienceVolume"));
        }
        original.SetMusicVolume(music);
        original.SetEffectsVolume(effects);
    }

    private static void CheckGain(string parameter, float volume)
    {
        var mixer = Read<AudioMixer>(original, "mixer");
        float expected = volume > 0.0001f ? Mathf.Log10(volume) * 20f : -80f;
        Require(mixer.GetFloat(parameter, out float actual) && Mathf.Abs(actual - expected) < 0.001f,
            "Exposed mixer gain matches slider: " + parameter + " = " + volume);
    }

    private static void CheckFarmActions()
    {
        var plot = UnityEngine.Object.FindFirstObjectByType<FarmPlot>();
        var inventory = UnityEngine.Object.FindFirstObjectByType<FarmInventory>();
        var tank = UnityEngine.Object.FindFirstObjectByType<FarmWaterTank>();
        var crop = AssetDatabase.LoadAssetAtPath<CropData>("Assets/_Project/Crops/TutorialCarrot.asset");
        Require(plot.TryPlant(crop, inventory, out _), "Prepare crop for action sounds");
        Require(plot.TryWater(tank, out _) && Read<AudioClip[]>(original, "wateringSounds").Contains(original.LastPlayedSound), "Successful watering chooses a watering variant");
        var effects = Read<AudioSource>(original, "effectsSource");
        effects.Stop();
        Require(!plot.TryFertilize(out _) && !effects.isPlaying, "Failed fertilizing does not play a success sound");
        plot.Machine.ChangeState(plot.Grown);
        Require(plot.TryHarvest(inventory, out _) && Read<AudioClip[]>(original, "harvestSounds").Contains(original.LastPlayedSound), "Successful harvest chooses a harvest variant");
        Require(plot.TryPlant(crop, inventory, out _), "Prepare crop for fertilizer sound");
        Require(plot.TryFertilize(out _) && Read<AudioClip[]>(original, "fertilizerSounds").Contains(original.LastPlayedSound), "Successful fertilizing chooses a fertilizer variant");
        effects.Stop();
        Require(!plot.TryFertilize(out _) && !effects.isPlaying, "Duplicate fertilizing does not play a success sound");
    }

    private static void CheckPurchasesAndMission()
    {
        var wallet = UnityEngine.Object.FindFirstObjectByType<FarmWallet>();
        var shop = UnityEngine.Object.FindFirstObjectByType<FarmShop>();
        var inventory = UnityEngine.Object.FindFirstObjectByType<FarmInventory>();
        wallet.AddCoins(10000);
        var technology = shop.GetTechnologyOffer(0);
        Require(shop.BuyTechnology(technology, out _) && original.LastPlayedSound.name == "SFX_UI_TechAdquired", "Market technology purchase uses its own sound");
        var effects = Read<AudioSource>(original, "effectsSource");
        effects.Stop();
        Require(!shop.BuyTechnology(technology, out _) && !effects.isPlaying, "Duplicate technology purchase is silent");
        var farmTechnology = AssetDatabase.LoadAssetAtPath<TechnologyData>("Assets/_Project/Tech/FarmUpgrades/Show Growing Time.asset");
        Require(shop.BuyTechnology(farmTechnology, out _) && original.LastPlayedSound.name == "SFX_UI_TechAdquired_01", "Farm upgrade uses the other technology sound");
        var crop = Read<CropData[]>(shop, "cropCatalog").First(shop.IsSeedAvailable);
        Require(shop.BuySeeds(crop, 1, out _) && original.LastPlayedSound.name == "SFX_UI_Coin_01", "Seed purchase uses purchase coin sound");
        inventory.AddProduce(crop, 1);
        Require(shop.SellProduce(crop, 1, out _) && original.LastPlayedSound.name == "SFX_UI_Coin_02", "Produce sale uses sale coin sound");
        var board = UnityEngine.Object.FindFirstObjectByType<FarmMissionBoard>();
        var mission = board.GetActiveMission(0);
        inventory.AddProduce(mission.requiredCrop, mission.requiredAmount);
        Require(board.Deliver(0, out _) && original.LastPlayedSound.name == "SFX_UI_CompleteMission_01", "Farm mission uses its own completion sound");
    }

    private static void CheckSound(Action action, string name)
    {
        Read<AudioSource>(original, "effectsSource").Stop();
        action();
        Require(original.LastPlayedSound != null && original.LastPlayedSound.name == name && Read<AudioSource>(original, "effectsSource").isPlaying,
            "Effect plays in its correct context: " + name);
    }

    private static void CheckRandom(Action action, string field, int count)
    {
        var clips = Read<AudioClip[]>(original, field);
        Require(clips.Length == count && clips.All(clip => clip != null), field + " contains all supplied versions");
        foreach (var clip in clips)
        {
            var samples = new float[clip.samples * clip.channels];
            Require(clip.GetData(samples, 0) && samples.Any(sample => Mathf.Abs(sample) > 0.0001f),
                "Imported action clip contains audible signal: " + clip.name);
        }
        var state = UnityEngine.Random.state;
        try
        {
            UnityEngine.Random.InitState(73);
            var selected = new System.Collections.Generic.HashSet<AudioClip>();
            AudioClip previous = null;
            for (int index = 0; index < 24; index++)
            {
                action();
                Require(clips.Contains(original.LastPlayedSound), "Random variant belongs to " + field);
                Require(previous == null || previous != original.LastPlayedSound, "Random action does not immediately repeat: " + field);
                previous = original.LastPlayedSound;
                selected.Add(original.LastPlayedSound);
            }
            Require(selected.Count == count, "All variants reached in " + field);
        }
        finally { UnityEngine.Random.state = state; }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        results.AppendLine("PASS: " + message);
    }

    private static T Read<T>(object target, string field) => (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Write(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);

    private static void Finish(int code)
    {
        FarmSaveChecks.CleanupTestKeys();
        AudioListener.volume = 1f;
        if (shortTrack != null) UnityEngine.Object.Destroy(shortTrack);
        if (longTrack != null) UnityEngine.Object.Destroy(longTrack);
        Directory.CreateDirectory(Report);
        File.WriteAllText(Report + "PlayModeChecks.txt", results + "Failures: " + code + "\n");
        SessionState.SetBool(Session, false);
        SessionState.SetInt("FarmAudioExit", code);
        EditorApplication.update -= Tick;
        EditorApplication.update += Exit;
        EditorApplication.isPlaying = false;
    }

    private static void Exit()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        EditorApplication.Exit(SessionState.GetInt("FarmAudioExit", 1));
    }
}
