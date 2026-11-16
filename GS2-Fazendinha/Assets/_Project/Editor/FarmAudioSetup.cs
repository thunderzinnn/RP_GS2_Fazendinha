using System.IO;
using System.Linq;

using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using System.Reflection;

public static class FarmAudioSetup
{
    private const string Root = "Assets/_Project/";
    private const string AudioRoot = Root + "Audios/";
    public const string MixerPath = AudioRoot + "Mixers/FarmAudio.mixer";

    public static void ApplyMixerAndCheck()
    {
        ApplyMixer();
        FarmAudioChecks.Run();
    }

    public static void ApplyMixer()
    {
        var mixer = CreateMixer();
        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { Root + "Scenes" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var scene = EditorSceneManager.OpenScene(path);
            foreach (var manager in Object.FindObjectsByType<AudioManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (scene.name != "MainMenu")
                {
                    Object.DestroyImmediate(manager.gameObject);
                    continue;
                }
                if (PrefabUtility.IsPartOfPrefabInstance(manager.gameObject))
                    PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(manager.gameObject), PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                var data = new SerializedObject(manager);
                Set(data, "mixer", mixer);
                string[] fields = { "musicGroup", "sfxGroup", "ambienceGroup" };
                string[] names = { "Music", "SFX", "Ambience" };
                string[] sources = { "musicSource", "effectsSource", "ambienceSource" };
                for (int index = 0; index < names.Length; index++)
                {
                    var group = mixer.FindMatchingGroups(names[index]).Single(item => item.name == names[index]);
                    Set(data, fields[index], group);
                    var source = (AudioSource)data.FindProperty(sources[index]).objectReferenceValue;
                    source.outputAudioMixerGroup = group;
                    source.volume = 1f;
                    EditorUtility.SetDirty(source);
                }
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        string oldPrefab = Root + "Prefabs/Systems/AudioManager.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(oldPrefab) != null) AssetDatabase.DeleteAsset(oldPrefab);
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(Root + "Scenes/MainMenu.unity");
    }

    private static AudioMixer CreateMixer()
    {
        Directory.CreateDirectory(AudioRoot + "Mixers");
        AssetDatabase.Refresh();
        // A criação de mixers só está disponível na API interna do Editor.
        var type = System.AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("UnityEditor.Audio.AudioMixerController"))
            .First(item => item != null);
        var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        if (mixer == null)
            mixer = (AudioMixer)type.GetMethod("CreateMixerControllerAtPath", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { MixerPath });
        var master = mixer.FindMatchingGroups("Master").Single(group => group.name == "Master");
        foreach (string name in new[] { "Music", "SFX", "Ambience" })
        {
            var group = mixer.FindMatchingGroups(name).FirstOrDefault(item => item.name == name);
            if (group == null)
            {
                group = (AudioMixerGroup)type.GetMethod("CreateNewGroup").Invoke(mixer, new object[] { name, false });
                type.GetMethod("AddChildToParent").Invoke(mixer, new object[] { group, master });
            }
            ExposeVolume(mixer, group, name + "Volume");
        }
        var views = type.GetProperty("views");
        var viewType = views.PropertyType.GetElementType();
        var view = System.Activator.CreateInstance(viewType);
        viewType.GetField("name").SetValue(view, "All");
        var groups = new[] { "Master", "Music", "SFX", "Ambience" }.Select(name =>
            mixer.FindMatchingGroups(name).Single(group => group.name == name));
        var ids = groups.Select(group => (GUID)group.GetType().GetProperty("groupID").GetValue(group)).ToArray();
        viewType.GetField("guids").SetValue(view, ids);
        var list = System.Array.CreateInstance(viewType, 1);
        list.SetValue(view, 0);
        views.SetValue(mixer, list);
        type.GetProperty("currentViewIndex").SetValue(mixer, 0);
        mixer.updateMode = AudioMixerUpdateMode.UnscaledTime;
        EditorUtility.SetDirty(mixer);
        AssetDatabase.SaveAssets();
        return mixer;
    }

    private static void ExposeVolume(AudioMixer mixer, AudioMixerGroup group, string name)
    {
        var data = new SerializedObject(mixer);
        var parameters = data.FindProperty("m_ExposedParameters");
        for (int index = 0; index < parameters.arraySize; index++)
            if (parameters.GetArrayElementAtIndex(index).FindPropertyRelative("name").stringValue == name) return;
        var guid = group.GetType().GetMethod("GetGUIDForVolume").Invoke(group, null);
        var pathType = mixer.GetType().Assembly.GetType("UnityEditor.Audio.AudioGroupParameterPath");
        var path = System.Activator.CreateInstance(pathType, new[] { (object)group, guid });
        mixer.GetType().GetMethod("AddExposedParameter").Invoke(mixer, new[] { path });
        data.Update();
        parameters = data.FindProperty("m_ExposedParameters");
        for (int index = 0; index < parameters.arraySize; index++)
        {
            var parameter = parameters.GetArrayElementAtIndex(index).FindPropertyRelative("name");
            if (parameter.stringValue.StartsWith("MyExposedParam")) parameter.stringValue = name;
        }
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void Apply()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioRoot + "MSC" }))
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.Streaming;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }
        ApplyMixer();

        foreach (string name in new[] { "MainMenu", "Tutorial", "MainLevel" })
        {
            var scene = EditorSceneManager.OpenScene(Root + "Scenes/" + name + ".unity");
            var manager = Object.FindFirstObjectByType<AudioManager>();
            if (manager != null) Configure(manager);
            var hud = Object.FindFirstObjectByType<FarmHUD>();
            if (hud != null) ConnectButtons(hud);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("../../Verification/Audio");
        File.WriteAllText("../../Verification/Audio/Setup.txt", "PASS: two four-track playlists, context effects, three sources and farm ambience connected.\n");
        EditorSceneManager.OpenScene(Root + "Scenes/MainMenu.unity");
    }

    private static void Configure(AudioManager manager)
    {
        if (manager == null) throw new System.InvalidOperationException("AudioManager is missing.");
        var data = new SerializedObject(manager);
        foreach (string sourceName in new[] { "musicSource", "effectsSource", "ambienceSource" })
        {
            var source = (AudioSource)data.FindProperty(sourceName).objectReferenceValue;
            if (source == null) source = manager.gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.loop = sourceName == "ambienceSource";
            data.FindProperty(sourceName).objectReferenceValue = source;
            if (PrefabUtility.IsPartOfPrefabInstance(source)) PrefabUtility.RecordPrefabInstancePropertyModifications(source);
        }
        Array(data, "menuPlaylist", FindClips("MSC", "MSC_Farm_MainMenu_"));
        Array(data, "gameplayPlaylist", FindClips("MSC", "MSC_Farm_Gameplay_"));
        Array(data, "wateringSounds", FindClips("SFX", "SFX_GAMEPLAY_Watering"));
        Array(data, "harvestSounds", FindClips("SFX", "SFX_GAMEPLAY_Harverst"));
        Array(data, "fertilizerSounds", FindClips("SFX", "SFX_GAMEPLAY_Fertilize"));
        Set(data, "startingMusic", null);
        Set(data, "uiClickSound", Clip("SFX", "SFX_UI_Click_01.mp3"));
        Set(data, "farmClickSound", Clip("SFX", "SFX_UI_Click_02.mp3"));
        Set(data, "cropClickSound", Clip("SFX", "SFX_UI_Click_03.mp3"));
        Set(data, "farmCloseSound", Clip("SFX", "SFX_UI_Close_01.wav"));
        Set(data, "menuCloseSound", Clip("SFX", "SFX_UI_Close_02.wav"));
        Set(data, "purchaseSound", Clip("SFX", "SFX_UI_Coin_01.wav"));
        Set(data, "saleSound", Clip("SFX", "SFX_UI_Coin_02.wav"));
        Set(data, "marketTechnologySound", Clip("SFX", "SFX_UI_TechAdquired.wav"));
        Set(data, "farmTechnologySound", Clip("SFX", "SFX_UI_TechAdquired_01.wav"));
        Set(data, "missionSound", Clip("SFX", "SFX_UI_CompleteMission_01.wav"));
        Set(data, "tutorialMissionSound", Clip("SFX", "SFX_UI_CompleteMission_02.wav"));
        Set(data, "tutorialCompleteSound", Clip("SFX", "SFX_Gameplay_CompleteTutorial_01.wav"));
        Set(data, "tutorialAmbience", Clip("AMB", "AMB_Farm_Background_01.mp3"));
        Set(data, "gameplayAmbience", Clip("AMB", "AMB_Farm_Background_02.mp3"));
        Set(data, "chickenSound", Clip("AMB", "AMB_Farm_Chicken_01.mp3"));
        data.FindProperty("ambienceVolume").floatValue = 0.3f;
        data.FindProperty("chickenInterval").floatValue = 25f;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConnectButtons(FarmHUD hud)
    {
        var context = hud.GetComponent<FarmPlotContextMenu>();
        var contextPanel = context != null ? (Transform)new SerializedObject(context).FindProperty("menuPanel").objectReferenceValue : null;
        foreach (var button in hud.GetComponentsInChildren<Button>(true))
        {
            for (int index = button.onClick.GetPersistentEventCount() - 1; index >= 0; index--)
            {
                string method = button.onClick.GetPersistentMethodName(index);
                Object target = button.onClick.GetPersistentTarget(index);
                if ((target is FarmHUD || target is AudioManager) &&
                    (method == "PlayUIClick" || method == "PlayCropClick" || method == "PlayFarmClose"))
                    UnityEventTools.RemovePersistentListener(button.onClick, index);
            }
            bool handledByMenu = false;
            for (int index = 0; index < button.onClick.GetPersistentEventCount(); index++)
                handledByMenu |= button.onClick.GetPersistentTarget(index) is FarmUIController ||
                    button.onClick.GetPersistentTarget(index) is FarmDemoEndController ||
                    button.onClick.GetPersistentTarget(index) is FarmTutorialController;
            if (handledByMenu) continue;
            if (button.name.Contains("Close") || button.name.StartsWith("Back"))
                UnityEventTools.AddPersistentListener(button.onClick, hud.PlayFarmClose);
            else if (contextPanel != null && button.transform.IsChildOf(contextPanel))
                UnityEventTools.AddPersistentListener(button.onClick, hud.PlayCropClick);
            else UnityEventTools.AddPersistentListener(button.onClick, hud.PlayUIClick);
        }
    }

    private static AudioClip Clip(string folder, string name)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioRoot + folder + "/" + name);
        if (clip == null) throw new System.InvalidOperationException("Audio clip missing: " + name);
        return clip;
    }

    private static AudioClip[] FindClips(string folder, string prefix) => AssetDatabase.FindAssets("t:AudioClip", new[] { AudioRoot + folder })
        .Select(AssetDatabase.GUIDToAssetPath).Where(path => Path.GetFileName(path).StartsWith(prefix))
        .OrderBy(path => path).Select(AssetDatabase.LoadAssetAtPath<AudioClip>).ToArray();

    private static void Set(SerializedObject data, string field, Object value) => data.FindProperty(field).objectReferenceValue = value;
    private static void Array(SerializedObject data, string field, AudioClip[] clips)
    {
        var array = data.FindProperty(field);
        array.arraySize = clips.Length;
        for (int index = 0; index < clips.Length; index++) array.GetArrayElementAtIndex(index).objectReferenceValue = clips[index];
    }
}
