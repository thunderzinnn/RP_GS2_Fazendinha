using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Linq;

public static class FarmCropModelInspection
{
    private const string PlayerSession = "PlayerAnimationCheck";
    private static StringBuilder playerReport;
    private static int playerPhase;
    private static double playerTime;
    private static int playerFrame;
    private static PlayerFarmController actor;
    private static FarmPlot targetPlot;
    private static bool sawWalk;

    public static void RunPlayerCheck()
    {
        SessionState.SetBool(PlayerSession, true);
        SessionState.SetString("PlayerAnimationPrefix", "GoldenRootTest_Player_" + System.Guid.NewGuid().ToString("N"));
        FarmSaveManager.KeyPrefix = SessionState.GetString("PlayerAnimationPrefix", "GoldenRootTest_Player");
        FarmSaveManager.AutomaticSavingEnabled = false;
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainMenu.unity");
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    private static void RestorePlayerCheck()
    {
        if (!SessionState.GetBool(PlayerSession, false)) return;
        FarmSaveManager.KeyPrefix = SessionState.GetString("PlayerAnimationPrefix", "GoldenRootTest_Player");
        FarmSaveManager.AutomaticSavingEnabled = false;
        playerReport = new StringBuilder();
        playerPhase = 0;
        playerTime = EditorApplication.timeSinceStartup;
        sawWalk = false;
        EditorApplication.update += TickPlayerCheck;
    }

    private static void TickPlayerCheck()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        double elapsed = EditorApplication.timeSinceStartup - playerTime;
        if (elapsed < (playerPhase == 2 || playerPhase == 3 ? 0.01d : 0.3d)) return;
        AudioListener.volume = 0f;
        try
        {
            if (playerPhase == 0)
            {
                if (elapsed < 1d) return;
                CheckMenuPlayer();
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainLevel");
                NextPlayerPhase(1);
            }
            else if (playerPhase == 1)
            {
                if (elapsed < 1d) return;
                actor = Object.FindFirstObjectByType<PlayerFarmController>();
                var animator = actor.GetComponentInChildren<Animator>();
                CheckBody(animator);
                CheckIdleCycle(animator);
                CapturePlayer("MainPlayerIdle");
                targetPlot = Object.FindObjectsByType<FarmPlot>(FindObjectsSortMode.None).First();
                var crop = AssetDatabase.LoadAssetAtPath<CropData>("Assets/_Project/Crops/Carrot.asset");
                actor.Inventory.AddSeeds(crop, 1);
                CheckPlayer(actor.RequestAction(targetPlot, FarmTool.Plant, crop), "Plant action queues through the existing player state machine");
                NextPlayerPhase(2);
            }
            else if (playerPhase == 2)
            {
                var animator = actor.GetComponentInChildren<Animator>();
                if (!sawWalk && animator.GetCurrentAnimatorStateInfo(0).IsName("Walk"))
                {
                    sawWalk = true;
                    CapturePlayer("MainPlayerWalk");
                }
                if (targetPlot.CurrentCrop == null)
                {
                    if (elapsed > 20d) throw new System.Exception("Player did not finish the planting path.");
                    return;
                }
                CheckPlayer(sawWalk, "Real navigation plays Walk");
                NextPlayerPhase(3);
            }
            else if (playerPhase == 3)
            {
                var animator = actor.GetComponentInChildren<Animator>();
                var state = animator.GetCurrentAnimatorStateInfo(0);
                if (!state.IsName("Planting2"))
                {
                    if (elapsed < 2d) return;
                    throw new System.Exception("Planting2 was not observed: state=" + state.shortNameHash + " time=" + state.normalizedTime + " moving=" + animator.GetBool("Moving") + " transition=" + animator.IsInTransition(0));
                }
                CheckPlayer(true, "Successful planting triggers Planting2");
                animator.Update(0.7f);
                CapturePlayer("MainPlayerPlanting");
                NextPlayerPhase(4);
            }
            else if (playerPhase == 4)
            {
                if (elapsed < FarmPresentationSetup.PlayerClip("Planting2").length + 0.25f) return;
                CheckPlayer(actor.GetComponentInChildren<Animator>().GetCurrentAnimatorStateInfo(0).IsTag("Idle"), "Planting2 returns to idle");
                CheckFacing();
                actor.GetComponentInChildren<Animator>().Play("Idle1", 0, 0f);
                CheckPlayer(actor.RequestAction(targetPlot, FarmTool.Water), "Water action queues");
                NextPlayerPhase(11);
            }
            else if (playerPhase == 5)
            {
                if (elapsed < 1d) return;
                actor = Object.FindFirstObjectByType<PlayerFarmController>();
                CheckBody(actor.GetComponentInChildren<Animator>());
                foreach (var tank in Object.FindObjectsByType<FarmWaterTank>(FindObjectsSortMode.None))
                    CheckPlayer(Mathf.Approximately(new SerializedObject(tank).FindProperty("regenerationInterval").floatValue, 3f), "Water regenerates every three seconds");
                targetPlot = Object.FindFirstObjectByType<FarmPlot>();
                var crop = AssetDatabase.LoadAssetAtPath<CropData>("Assets/_Project/Crops/TutorialCarrot.asset");
                actor.Inventory.AddSeeds(crop, 1);
                CheckPlayer(targetPlot.TryPlant(crop, actor.Inventory, out _), "Tutorial planting uses its current CropData");
                NextPlayerPhase(6);
            }
            else if (playerPhase >= 6 && playerPhase <= 9)
            {
                if (Time.frameCount <= playerFrame + 2) return;
                CheckCropGround();
                CapturePlayer(new[] { "TutorialSeed", "TutorialGrowing", "TutorialGrown", "TutorialDead" }[playerPhase - 6]);
                if (playerPhase == 6) targetPlot.Machine.ChangeState(targetPlot.Growing);
                else if (playerPhase == 7) targetPlot.Machine.ChangeState(targetPlot.Grown);
                else if (playerPhase == 8) targetPlot.Machine.ChangeState(targetPlot.Dead);
                else
                {
                    UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
                    NextPlayerPhase(10);
                    return;
                }
                NextPlayerPhase(playerPhase + 1);
            }
            else if (playerPhase == 10)
            {
                if (elapsed < 1d) return;
                CheckMenuPlayer();
                CapturePlayer("MenuPlayer");
                FinishPlayerCheck(0);
            }
            else if (playerPhase == 11 || playerPhase == 12)
            {
                var animator = actor.GetComponentInChildren<Animator>();
                if (!animator.GetCurrentAnimatorStateInfo(0).IsName("Planting2"))
                {
                    if (elapsed < 5d) return;
                    throw new System.Exception("Action animation missing: " + playerPhase);
                }
                CheckPlayer(true, playerPhase == 11 ? "Water plays Planting2" : "Harvest plays Planting2");
                CheckFacing();
                if (playerPhase == 11)
                {
                    targetPlot.Machine.ChangeState(targetPlot.Grown);
                    animator.Play("Idle1", 0, 0f);
                    animator.Update(0f);
                    CheckPlayer(actor.RequestAction(targetPlot, FarmTool.Harvest), "Harvest action queues");
                    NextPlayerPhase(12);
                }
                else
                {
                    UnityEngine.SceneManagement.SceneManager.LoadScene("Tutorial");
                    NextPlayerPhase(5);
                }
            }
        }
        catch (System.Exception error)
        {
            playerReport.AppendLine("FAIL: " + error);
            FinishPlayerCheck(1);
        }
    }

    private static void CheckIdleCycle(Animator animator)
    {
        animator.SetBool("Moving", false);
        foreach (int index in new[] { 1, 2, 3 })
        {
            animator.Play("Idle" + index, 0, 0f);
            animator.Update(0f);
            Advance(animator, index == 1 ? 7.6f : 3.6f);
            CheckPlayer(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle" + index), "Idle" + index + " holds for its configured duration");
            Advance(animator, 0.65f);
            CheckPlayer(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle" + (index % 3 + 1)), "Idle" + index + " cycles to the next idle");
        }
        animator.SetBool("Moving", true);
        Advance(animator, 0.3f);
        CheckPlayer(animator.GetCurrentAnimatorStateInfo(0).IsName("Walk"), "Moving interrupts the idle cycle");
        animator.SetBool("Moving", false);
        Advance(animator, 0.3f);
        CheckPlayer(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle1"), "Stopping returns to Idle1");
        animator.SetTrigger("Plant");
        Advance(animator, 0.3f);
        CheckPlayer(animator.GetCurrentAnimatorStateInfo(0).IsName("Planting2"), "Plant trigger enters Planting2");
        Advance(animator, FarmPresentationSetup.PlayerClip("Planting2").length + 0.3f);
    }

    private static void Advance(Animator animator, float seconds)
    {
        while (seconds > 0f)
        {
            float step = Mathf.Min(0.05f, seconds);
            animator.Update(step);
            seconds -= step;
        }
    }

    private static void CheckBody(Animator animator)
    {
        CheckPlayer(animator != null && animator.runtimeAnimatorController != null && !animator.applyRootMotion, "Animator is connected without moving the NavMesh root");
        var skins = animator.GetComponentsInChildren<SkinnedMeshRenderer>();
        CheckPlayer(skins.Length > 0 && skins.All(skin => skin.sharedMaterial != null && skin.sharedMaterial.mainTexture != null), "Character uses the supplied texture atlas");
        var bounds = SkinBounds(animator.gameObject);
        playerReport.AppendLine("BODY bounds=" + bounds);
        CheckPlayer(bounds.size.y > 1.2f && bounds.size.y < 3f, "Character scale matches the existing player");
        var bones = animator.GetComponentsInChildren<Transform>();
        var foot = bones.First(node => node.name == "Foot.L");
        var ankle = bones.First(node => node.name == "Ankle.L");
        var forward = animator.transform.parent.parent.forward;
        playerReport.AppendLine("FOOT facing=" + Vector3.Dot((foot.position - ankle.position).normalized, forward));
    }

    private static void CheckFacing()
    {
        Vector3 direction = targetPlot.transform.position - actor.transform.position;
        direction.y = 0f;
        playerReport.AppendLine("FACING actor=" + actor.transform.position + " plot=" + targetPlot.transform.position + " forward=" + actor.transform.forward + " expected=" + direction.normalized + " dot=" + Vector3.Dot(actor.transform.forward, direction.normalized) + " agentRotation=" + actor.Agent.updateRotation);
        CheckPlayer(Vector3.Dot(actor.transform.forward, direction.normalized) > 0.95f, "Player faces the plot during the action");
    }

    private static void CheckMenuPlayer()
    {
        var menu = Object.FindFirstObjectByType<MenuFarmBackdrop>();
        var animator = (Animator)new SerializedObject(menu).FindProperty("farmerAnimator").objectReferenceValue;
        CheckBody(animator);
        CheckPlayer(animator.updateMode == AnimatorUpdateMode.UnscaledTime, "Menu character uses unscaled animation time");
        float ground = FarmCropPrefabRefresh.BoundsOf(menu.GetComponentsInChildren<Transform>(true).First(node => node.name == "Ground").gameObject).max.y;
        CheckPlayer(Mathf.Abs(SkinBounds(animator.gameObject).min.y - ground) < 0.25f, "Menu character stands above the ground");
    }

    private static void CheckCropGround()
    {
        var model = (GameObject)typeof(FarmPlot).GetField("currentModelInstance", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(targetPlot);
        float ground = targetPlot.transform.Find("SoilMesh").GetComponent<Renderer>().bounds.max.y;
        foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            CheckPlayer(Mathf.Abs(renderer.bounds.min.y - ground) < 0.02f, targetPlot.StateName + ": crop base meets the soil");
    }

    private static void CapturePlayer(string name)
    {
        SessionState.SetString("FarmSystemAuditReport", "../../Verification/PlayerAnimation");
        FarmSystemChecks.Capture(name);
    }

    private static void NextPlayerPhase(int phase)
    {
        playerPhase = phase;
        playerTime = EditorApplication.timeSinceStartup;
        playerFrame = Time.frameCount;
    }

    private static void CheckPlayer(bool valid, string message)
    {
        playerReport.AppendLine((valid ? "PASS: " : "FAIL: ") + message);
        if (!valid) throw new System.Exception(message);
    }

    private static void FinishPlayerCheck(int code)
    {
        FarmSaveChecks.CleanupTestKeys();
        AudioListener.volume = 1f;
        File.WriteAllText("../../Verification/PlayerAnimation/PlayModeChecks.txt", playerReport + "Failures: " + code);
        SessionState.SetBool(PlayerSession, false);
        SessionState.SetInt("PlayerAnimationExit", code);
        EditorApplication.update -= TickPlayerCheck;
        EditorApplication.update += ExitPlayerCheck;
        EditorApplication.isPlaying = false;
    }

    private static void ExitPlayerCheck()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        EditorApplication.Exit(SessionState.GetInt("PlayerAnimationExit", 1));
    }

    public static void InspectPlayer()
    {
        var report = new StringBuilder();
        string path = "Assets/_Project/Models/Player/Character_Models_Animations.fbx";
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            report.AppendLine(asset.GetType().Name + " | " + asset.name);
            if (!(asset is AnimationClip clip) || clip.name.StartsWith("__preview__")) continue;
            report.AppendLine("  duration=" + clip.length + " fps=" + clip.frameRate + " legacy=" + clip.legacy + " loop=" + clip.isLooping);
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                report.AppendLine("  " + binding.path + " | " + binding.propertyName);
        }
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        foreach (var clip in importer.defaultClipAnimations)
            report.AppendLine("TAKE " + clip.name + " | " + clip.firstFrame + ".." + clip.lastFrame + " loop=" + clip.loopTime);
        var preview = EditorSceneManager.NewPreviewScene();
        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), preview);
        try
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (!(asset is AnimationClip clip) || clip.name.StartsWith("__preview__")) continue;
                foreach (float fraction in new[] { 0f, 0.5f, 0.99f })
                {
                    clip.SampleAnimation(model, clip.length * fraction);
                    report.AppendLine("POSE " + clip.name + " t=" + fraction + " bounds=" + SkinBounds(model));
                }
            }
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is AnimationClip clip && clip.name == "Armature|Idle1") clip.SampleAnimation(model, 0f);
            var skin = SkinBounds(model);
            model.transform.position -= new Vector3(skin.center.x, skin.min.y, skin.center.z);
            var bounds = FarmCropPrefabRefresh.BoundsOf(model);
            report.AppendLine("MODEL bounds=" + bounds + " scale=" + model.transform.localScale);
            foreach (var node in model.GetComponentsInChildren<Transform>(true))
                report.AppendLine("NODE " + node.name + " position=" + node.localPosition + " rotation=" + node.localEulerAngles + " scale=" + node.localScale);
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                    report.AppendLine("MATERIAL " + material.name + " shader=" + material.shader.name + " texture=" + (material.mainTexture != null ? material.mainTexture.name : "NONE"));
            var camera = new GameObject("PreviewCamera").AddComponent<Camera>();
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camera.gameObject, preview);
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(preview);
            camera.transform.position = bounds.center + new Vector3(2f, 1f, 3f) * bounds.size.y * 0.8f;
            camera.transform.LookAt(bounds.center);
            camera.fieldOfView = 35f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = Mathf.Max(1000f, bounds.size.y * 20f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(54, 62, 58, 255);
            var light = new GameObject("PreviewLight").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            light.transform.rotation = Quaternion.Euler(35f, -35f, 0f);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(light.gameObject, preview);
            var target = new RenderTexture(640, 800, 24);
            var image = new Texture2D(640, 800, TextureFormat.RGB24, false);
            var active = RenderTexture.active;
            camera.targetTexture = target;
            camera.Render(); camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 640, 800), 0, 0); image.Apply();
            Directory.CreateDirectory("../../Verification/PlayerAnimation");
            File.WriteAllBytes("../../Verification/PlayerAnimation/Model.png", image.EncodeToPNG());
            camera.targetTexture = null; RenderTexture.active = active;
            target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(image);
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
        foreach (string name in new[] { "MainLevel", "Tutorial", "MainMenu" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/" + name + ".unity");
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var player in root.GetComponentsInChildren<PlayerFarmController>(true))
                {
                    var agent = player.GetComponent<UnityEngine.AI.NavMeshAgent>();
                    report.AppendLine(name + " PLAYER " + player.name + " position=" + player.transform.position + " scale=" + player.transform.lossyScale + " baseOffset=" + agent.baseOffset + " height=" + agent.height);
                    foreach (var renderer in player.GetComponentsInChildren<Renderer>(true)) report.AppendLine("  " + renderer.name + " bounds=" + renderer.bounds);
                }
                foreach (var plot in root.GetComponentsInChildren<FarmPlot>(true))
                    report.AppendLine(name + " PLOT " + plot.name + " anchor=" + ((Transform)new SerializedObject(plot).FindProperty("cropVisualAnchor").objectReferenceValue).position + " soil=" + plot.transform.Find("SoilMesh").GetComponent<Renderer>().bounds);
            }
            var menu = Object.FindFirstObjectByType<FarmPlotContextMenu>(FindObjectsInactive.Include);
            if (menu != null)
            {
                var crops = new SerializedObject(menu).FindProperty("availableCrops");
                for (int index = 0; index < crops.arraySize; index++)
                {
                    var crop = (CropData)crops.GetArrayElementAtIndex(index).objectReferenceValue;
                    if (crop == null) continue;
                    report.AppendLine(name + " CROP " + AssetDatabase.GetAssetPath(crop) + " stages=" + AssetDatabase.GetAssetPath(crop.plantedPrefab) + " | " + AssetDatabase.GetAssetPath(crop.growingPrefab) + " | " + AssetDatabase.GetAssetPath(crop.grownPrefab));
                }
            }
        }
        File.WriteAllText("../../Verification/PlayerAnimation/Inspection.txt", report.ToString());
    }

    public static Bounds SkinBounds(GameObject model)
    {
        var bounds = new Bounds();
        bool first = true;
        foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var mesh = new Mesh();
            renderer.BakeMesh(mesh, true);
            foreach (var vertex in mesh.vertices)
            {
                var point = renderer.transform.TransformPoint(vertex);
                if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                else bounds.Encapsulate(point);
            }
            Object.DestroyImmediate(mesh);
        }
        return bounds;
    }

    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainLevel.unity");
        var preview = EditorSceneManager.NewPreviewScene();
        var report = new StringBuilder();
        foreach (string folder in new[] { "Assets/_Project/Models/Crops", "Assets/_Project/Prefabs/Seed", "Assets/_Project/Prefabs/Growing" })
        {
            foreach (string guid in AssetDatabase.FindAssets("t:GameObject", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var root = (GameObject)PrefabUtility.InstantiatePrefab(asset, preview);
                root.transform.position = Vector3.zero;
                report.AppendLine(path + " | scale=" + root.transform.localScale + " | children=" + root.transform.childCount);
                var renderers = root.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length > 0)
                {
                    var bounds = renderers[0].bounds;
                    foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                    report.AppendLine("  bounds center=" + bounds.center.ToString("F3") + " size=" + bounds.size.ToString("F3") + " min=" + bounds.min.ToString("F3"));
                }
                foreach (Transform child in root.transform)
                    report.AppendLine("  child " + child.name + " position=" + child.localPosition.ToString("F3") + " scale=" + child.localScale.ToString("F3"));
                foreach (var renderer in renderers)
                    foreach (var material in renderer.sharedMaterials)
                        report.AppendLine("  material " + (material != null ? material.name + " | shader=" + material.shader.name + " | texture=" + (material.mainTexture != null ? material.mainTexture.name : "NONE") : "MISSING"));
                Object.DestroyImmediate(root);
            }
        }
        foreach (var root in scene.GetRootGameObjects())
            foreach (var plot in root.GetComponentsInChildren<FarmPlot>(true))
            {
                var soil = plot.transform.Find("SoilMesh").GetComponent<Renderer>();
                report.AppendLine("PLOT " + plot.name + " position=" + plot.transform.position + " soil bounds=" + soil.bounds);
            }
        EditorSceneManager.ClosePreviewScene(preview);
        Directory.CreateDirectory("../../Verification/CropGroups");
        File.WriteAllText("../../Verification/CropGroups/ModelInspection.txt", report.ToString());
        Debug.Log("Crop model inspection completed.");
    }
}
