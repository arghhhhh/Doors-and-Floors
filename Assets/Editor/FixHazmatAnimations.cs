using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

/// <summary>
/// Fixes Hazmat Man T-pose by properly configuring FBX import settings via ModelImporter API
/// and rebuilding the AnimatorController with non-legacy Generic animation clips.
/// Run via: Tools > Fix Hazmat Man Animations
/// </summary>
public static class FixHazmatAnimations
{
    const string MODEL_BASE   = "Assets/Models/Meshy_AI_hazmat_man_1_biped_separate/Meshy_AI_hazmat_man_1_biped/";
    const string ANIM_RUNNING = MODEL_BASE + "Meshy_AI_hazmat_man_1_biped_Animation_Running_withSkin.fbx";
    const string ANIM_WALK_IP = MODEL_BASE + "Meshy_AI_hazmat_man_1_biped_Animation_walking_2_inplace_withSkin.fbx";
    const string ANIM_JUMP    = MODEL_BASE + "Meshy_AI_hazmat_man_1_biped_Animation_Regular_Jump_withSkin.fbx";
    const string ANIM_FALL    = MODEL_BASE + "Meshy_AI_hazmat_man_1_biped_Animation_Jumping_Down_withSkin.fbx";
    const string CHAR_FBX     = MODEL_BASE + "Meshy_AI_hazmat_man_1_biped_Character_output.fbx";

    const string CONTROLLER_PATH = "Assets/Animation/HazmatManController.controller";

    [MenuItem("Tools/Fix Hazmat Man Animations")]
    public static void Run()
    {
        Debug.Log("[FixHazmatAnimations] Starting animation fix...");

        // Step 1: Set all animation FBXes to Generic rig with proper clip extraction
        ConfigureFbx(ANIM_WALK_IP, "walking_2_inplace", loop: true);
        ConfigureFbx(ANIM_RUNNING, "running",            loop: true);
        ConfigureFbx(ANIM_JUMP,   "Regular_Jump",        loop: false);
        ConfigureFbx(ANIM_FALL,   "Jumping_Down",        loop: false);

        // Also fix character FBX to Generic with no legacy animations
        ConfigureCharacterFbx(CHAR_FBX);

        // Force reimport
        AssetDatabase.Refresh();
        Debug.Log("[FixHazmatAnimations] FBX reimport triggered. Waiting...");

        // Step 2 will be called by the user again after reimport, but we can also
        // immediately try to load and build the controller. Since Refresh is synchronous
        // for most purposes in editor scripts, proceed:
        BuildController();

        // Step 3: Fix player animators
        FixPlayerAnimator("Player1");
        FixPlayerAnimator("Player2");

        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        Debug.Log("[FixHazmatAnimations] Done!");
    }

    /// <summary>
    /// Only rebuild the controller (run this after FBX reimport completes).
    /// </summary>
    [MenuItem("Tools/Fix Hazmat Man Animations - Step 2 Only")]
    public static void RebuildControllerOnly()
    {
        BuildController();
        FixPlayerAnimator("Player1");
        FixPlayerAnimator("Player2");
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("[FixHazmatAnimations] Controller rebuild done!");
    }

    static void ConfigureFbx(string path, string clipName, bool loop)
    {
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError($"[FixHazmatAnimations] Cannot get ModelImporter for: {path}");
            return;
        }

        // Set to Generic rig (no humanoid bone mapping needed)
        importer.animationType = ModelImporterAnimationType.Generic;

        // Get available takes from the FBX
        TakeInfo[] takes = importer.importedTakeInfos;
        Debug.Log($"[FixHazmatAnimations] {System.IO.Path.GetFileName(path)}: found {takes.Length} takes");

        if (takes.Length == 0)
        {
            Debug.LogWarning($"[FixHazmatAnimations] No takes found in {path} — cannot configure clips.");
            importer.SaveAndReimport();
            return;
        }

        // Build clip animations from the first take (these FBXes have one take each)
        TakeInfo take = takes[0];
        Debug.Log($"[FixHazmatAnimations]   Take: '{take.name}' frames {take.startTime * take.sampleRate:F0}-{take.stopTime * take.sampleRate:F0} @ {take.sampleRate}fps");

        var clip = new ModelImporterClipAnimation();
        clip.name         = clipName;
        clip.takeName     = take.name;
        clip.firstFrame   = take.startTime * take.sampleRate;
        clip.lastFrame    = take.stopTime  * take.sampleRate;
        clip.loopTime     = loop;
        clip.wrapMode     = loop ? WrapMode.Loop : WrapMode.Once;

        importer.clipAnimations = new[] { clip };
        importer.SaveAndReimport();
        Debug.Log($"[FixHazmatAnimations] Configured clip '{clipName}' (loop={loop}) from take '{take.name}'");
    }

    static void ConfigureCharacterFbx(string path)
    {
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError($"[FixHazmatAnimations] Cannot get ModelImporter for: {path}");
            return;
        }

        importer.animationType = ModelImporterAnimationType.Generic;
        // Disable animation import on the character mesh FBX - it only has a bind-pose take
        importer.importAnimation = false;
        importer.SaveAndReimport();
        Debug.Log("[FixHazmatAnimations] Character FBX: set to Generic, importAnimation=false");
    }

    static void BuildController()
    {
        AnimationClip clipIdle = LoadClipByName(ANIM_WALK_IP, "walking_2_inplace");
        AnimationClip clipRun  = LoadClipByName(ANIM_RUNNING, "running");
        AnimationClip clipJump = LoadClipByName(ANIM_JUMP,   "Regular_Jump");
        AnimationClip clipFall = LoadClipByName(ANIM_FALL,   "Jumping_Down");

        LogClipInfo("Idle", clipIdle);
        LogClipInfo("Run",  clipRun);
        LogClipInfo("Jump", clipJump);
        LogClipInfo("Fall", clipFall);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(CONTROLLER_PATH);

        controller.AddParameter("Speed",      AnimatorControllerParameterType.Float);
        controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsJumping",  AnimatorControllerParameterType.Bool);

        var sm = controller.layers[0].stateMachine;

        var stateIdle = sm.AddState("Idle");
        var stateRun  = sm.AddState("Run");
        var stateJump = sm.AddState("Jump");
        var stateFall = sm.AddState("Fall");

        if (clipIdle != null) stateIdle.motion = clipIdle;
        if (clipRun  != null) stateRun.motion  = clipRun;
        if (clipJump != null) stateJump.motion  = clipJump;
        if (clipFall != null) stateFall.motion  = clipFall;

        sm.defaultState = stateIdle;

        var idleToRun = stateIdle.AddTransition(stateRun);
        idleToRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        idleToRun.hasExitTime = false; idleToRun.duration = 0.1f;

        var runToIdle = stateRun.AddTransition(stateIdle);
        runToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        runToIdle.hasExitTime = false; runToIdle.duration = 0.1f;

        var idleToJump = stateIdle.AddTransition(stateJump);
        idleToJump.AddCondition(AnimatorConditionMode.If, 0, "IsJumping");
        idleToJump.hasExitTime = false; idleToJump.duration = 0.05f;

        var runToJump = stateRun.AddTransition(stateJump);
        runToJump.AddCondition(AnimatorConditionMode.If, 0, "IsJumping");
        runToJump.hasExitTime = false; runToJump.duration = 0.05f;

        var jumpToFall = stateJump.AddTransition(stateFall);
        jumpToFall.hasExitTime = true; jumpToFall.exitTime = 0.9f; jumpToFall.duration = 0.1f;

        var jumpToFallEarly = stateJump.AddTransition(stateFall);
        jumpToFallEarly.AddCondition(AnimatorConditionMode.IfNot, 0, "IsGrounded");
        jumpToFallEarly.hasExitTime = true; jumpToFallEarly.exitTime = 0.5f; jumpToFallEarly.duration = 0.1f;

        var fallToIdle = stateFall.AddTransition(stateIdle);
        fallToIdle.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");
        fallToIdle.hasExitTime = false; fallToIdle.duration = 0.1f;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log($"[FixHazmatAnimations] AnimatorController rebuilt: {CONTROLLER_PATH}");
    }

    static void FixPlayerAnimator(string playerName)
    {
        var go = GameObject.Find(playerName);
        if (go == null) { Debug.LogWarning($"[FixHazmatAnimations] Cannot find '{playerName}'"); return; }

        var anim = go.GetComponent<Animator>();
        if (anim == null) { Debug.LogWarning($"[FixHazmatAnimations] No Animator on '{playerName}'"); return; }

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CONTROLLER_PATH);
        anim.runtimeAnimatorController = controller;
        anim.avatar = null;
        anim.applyRootMotion = false;
        EditorUtility.SetDirty(go);
        Debug.Log($"[FixHazmatAnimations] Fixed Animator on '{playerName}'");
    }

    static AnimationClip LoadClipByName(string fbxPath, string clipName)
    {
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            if (a is AnimationClip c && c.name == clipName)
                return c;
        // Fallback: first non-preview clip
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            if (a is AnimationClip c && !c.name.StartsWith("__preview__"))
                return c;
        return null;
    }

    static void LogClipInfo(string label, AnimationClip clip)
    {
        if (clip == null)
            Debug.LogWarning($"[FixHazmatAnimations] {label}: NOT FOUND");
        else
            Debug.Log($"[FixHazmatAnimations] {label}: '{clip.name}' ({clip.length:F2}s, legacy={clip.legacy}, loop={clip.isLooping})");
    }
}
