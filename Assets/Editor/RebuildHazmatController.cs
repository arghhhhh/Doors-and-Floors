using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

/// <summary>
/// Rebuilds the HazmatManController AnimatorController with proper clips, parameters,
/// states, and transitions. Also fixes FBX import settings so all clips loop correctly
/// and the Regular_Jump FBX is split into JumpUp and JumpDown sub-clips.
///
/// Run via: Tools > Rebuild Hazmat Controller
/// </summary>
public static class RebuildHazmatController
{
    const string MODEL_BASE = "Assets/Models/Meshy_AI_hazmat_man_1_biped_separate/Meshy_AI_hazmat_man_1_biped/";

    const string FBX_CHAR    = MODEL_BASE + "Meshy_AI_hazmat_man_1_biped_Character_output.fbx";
    const string FBX_IDLE    = MODEL_BASE + "Meshy_AI_hazmat_man_1_biped_Animation_Idle_02_withSkin.fbx";
    const string FBX_WALK    = MODEL_BASE + "Meshy_AI_hazmat_man_1_biped_Animation_Walking_withSkin.fbx";
    const string FBX_RUN     = MODEL_BASE + "Meshy_AI_hazmat_man_1_biped_Animation_Running_withSkin.fbx";
    const string FBX_JUMP    = MODEL_BASE + "Meshy_AI_hazmat_man_1_biped_Animation_Regular_Jump_withSkin.fbx";

    const string CONTROLLER_PATH = "Assets/Animation/HazmatManController.controller";

    [MenuItem("Tools/Rebuild Hazmat Controller")]
    public static void Run()
    {
        Debug.Log("[RebuildHazmatController] Starting...");

        // Step 1: Convert character model to Humanoid first (generates the avatar)
        FixCharacterFbx();

        // Step 2: Fix animation FBX import settings (Humanoid, copy avatar from character)
        FixIdleFbx();
        FixWalkFbx();
        FixRunFbx();
        FixJumpFbx();

        // Force reimport so the new clip settings take effect
        AssetDatabase.Refresh();
        Debug.Log("[RebuildHazmatController] FBX reimport triggered. Building controller...");

        // Step 2: Rebuild the AnimatorController
        BuildController();

        // Step 3: Re-point scene animators to the controller
        FixPlayerAnimator("Player1", "/Player1/HazmatManModel");
        FixPlayerAnimator("Player2", "/Player2/HazmatManModel");

        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        Debug.Log("[RebuildHazmatController] Done!");
    }

    // -------------------------------------------------------------------------
    // FBX Import Fixers
    // -------------------------------------------------------------------------

    static void FixCharacterFbx()
    {
        var imp = GetImporter(FBX_CHAR);
        if (imp == null) return;

        imp.animationType = ModelImporterAnimationType.Human;
        imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        imp.SaveAndReimport();
        Debug.Log("[RebuildHazmatController] Character FBX set to Humanoid (create avatar from this model)");
    }

    static void SetupHumanoidAnimation(ModelImporter imp)
    {
        imp.animationType = ModelImporterAnimationType.Human;
        imp.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
        imp.sourceAvatar = LoadCharacterAvatar();
    }

    static Avatar LoadCharacterAvatar()
    {
        // Load the avatar generated from the character FBX
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(FBX_CHAR))
        {
            if (asset is Avatar avatar)
                return avatar;
        }
        Debug.LogWarning("[RebuildHazmatController] No Avatar found in character FBX. Run this tool again after reimport.");
        return null;
    }

    static void FixIdleFbx()
    {
        // Idle_02: take "Armature|Armature|Idle_02|baselayer", frames 0-70, LOOP
        var imp = GetImporter(FBX_IDLE);
        if (imp == null) return;

        SetupHumanoidAnimation(imp);

        var clip = new ModelImporterClipAnimation();
        clip.name       = "idle";
        clip.takeName   = "Armature|Armature|Idle_02|baselayer";
        clip.firstFrame = 0;
        clip.lastFrame  = 70;
        clip.loopTime   = true;
        clip.wrapMode   = WrapMode.Loop;
        clip.lockRootHeightY = true;
        clip.keepOriginalPositionY = true;
        clip.heightFromFeet = true;

        imp.clipAnimations = new[] { clip };
        imp.SaveAndReimport();
        Debug.Log("[RebuildHazmatController] Fixed Idle_02 FBX (loop=true)");
    }

    static void FixWalkFbx()
    {
        // Walking: take "Armature|Armature|walking_man|baselayer", frames 0-31, LOOP
        var imp = GetImporter(FBX_WALK);
        if (imp == null) return;

        SetupHumanoidAnimation(imp);

        var clip = new ModelImporterClipAnimation();
        clip.name       = "walking";
        clip.takeName   = "Armature|Armature|walking_man|baselayer";
        clip.firstFrame = 0;
        clip.lastFrame  = 31;
        clip.loopTime   = true;
        clip.wrapMode   = WrapMode.Loop;
        clip.lockRootHeightY = true;
        clip.keepOriginalPositionY = true;
        clip.heightFromFeet = true;

        imp.clipAnimations = new[] { clip };
        imp.SaveAndReimport();
        Debug.Log("[RebuildHazmatController] Fixed Walking FBX (loop=true)");
    }

    static void FixRunFbx()
    {
        // Running: take "Armature|Armature|running|baselayer", frames 0-19, LOOP (already correct but re-assert)
        var imp = GetImporter(FBX_RUN);
        if (imp == null) return;

        SetupHumanoidAnimation(imp);

        var clip = new ModelImporterClipAnimation();
        clip.name       = "running";
        clip.takeName   = "Armature|Armature|running|baselayer";
        clip.firstFrame = 0;
        clip.lastFrame  = 19;
        clip.loopTime   = true;
        clip.wrapMode   = WrapMode.Loop;
        clip.lockRootHeightY = true;
        clip.keepOriginalPositionY = true;
        clip.heightFromFeet = true;

        imp.clipAnimations = new[] { clip };
        imp.SaveAndReimport();
        Debug.Log("[RebuildHazmatController] Fixed Running FBX (loop=true)");
    }

    static void FixJumpFbx()
    {
        // Regular_Jump: take "Armature|Armature|Regular_Jump|baselayer", frames 0-57
        // Split into:
        //   JumpUp   : frames 5-22  (no loop, skip pre-jump squat)
        //   JumpDown : frames 23-57 (loop, for extended falling)
        // Bake Into Pose Y ON + heightFromFeet to match grounded clips
        var imp = GetImporter(FBX_JUMP);
        if (imp == null) return;

        SetupHumanoidAnimation(imp);

        var jumpUp = new ModelImporterClipAnimation();
        jumpUp.name       = "JumpUp";
        jumpUp.takeName   = "Armature|Armature|Regular_Jump|baselayer";
        jumpUp.firstFrame = 5;
        jumpUp.lastFrame  = 22;
        jumpUp.loopTime   = false;
        jumpUp.wrapMode   = WrapMode.Once;
        jumpUp.lockRootHeightY = true;
        jumpUp.keepOriginalPositionY = true;
        jumpUp.heightFromFeet = true;

        var jumpDown = new ModelImporterClipAnimation();
        jumpDown.name       = "JumpDown";
        jumpDown.takeName   = "Armature|Armature|Regular_Jump|baselayer";
        jumpDown.firstFrame = 23;
        jumpDown.lastFrame  = 57;
        jumpDown.loopTime   = true;
        jumpDown.wrapMode   = WrapMode.Loop;
        jumpDown.lockRootHeightY = true;
        jumpDown.keepOriginalPositionY = true;
        jumpDown.heightFromFeet = true;

        imp.clipAnimations = new[] { jumpUp, jumpDown };
        imp.SaveAndReimport();
        Debug.Log("[RebuildHazmatController] Fixed Regular_Jump FBX: split into JumpUp(5-22) and JumpDown(23-57)");
    }

    // -------------------------------------------------------------------------
    // Controller Builder
    // -------------------------------------------------------------------------

    static void BuildController()
    {
        AnimationClip clipIdle     = LoadClip(FBX_IDLE, "idle");
        AnimationClip clipWalk     = LoadClip(FBX_WALK, "walking");
        AnimationClip clipRun      = LoadClip(FBX_RUN,  "running");
        AnimationClip clipJumpUp   = LoadClip(FBX_JUMP, "JumpUp");
        AnimationClip clipJumpDown = LoadClip(FBX_JUMP, "JumpDown");

        LogClip("Idle",     clipIdle);
        LogClip("Walk",     clipWalk);
        LogClip("Run",      clipRun);
        LogClip("JumpUp",   clipJumpUp);
        LogClip("JumpDown", clipJumpDown);

        // Overwrite the existing controller at the same path so scene references are preserved
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(CONTROLLER_PATH);

        // Parameters
        controller.AddParameter("Speed",      AnimatorControllerParameterType.Float);
        controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter("VelocityY",  AnimatorControllerParameterType.Float);

        var sm = controller.layers[0].stateMachine;

        // States
        var stateIdle     = sm.AddState("Idle");
        var stateWalk     = sm.AddState("Walk");
        var stateRun      = sm.AddState("Run");
        var stateJumpUp   = sm.AddState("JumpUp");
        var stateJumpDown = sm.AddState("JumpDown");

        if (clipIdle     != null) stateIdle.motion     = clipIdle;
        if (clipWalk     != null) stateWalk.motion     = clipWalk;
        if (clipRun      != null) stateRun.motion      = clipRun;
        if (clipJumpUp   != null) stateJumpUp.motion   = clipJumpUp;
        if (clipJumpDown != null) stateJumpDown.motion = clipJumpDown;

        sm.defaultState = stateIdle;

        // ----- Ground locomotion transitions -----

        // Idle -> Walk: Speed > 0.1 AND IsGrounded
        var idleToWalk = stateIdle.AddTransition(stateWalk);
        idleToWalk.hasExitTime = false;
        idleToWalk.duration    = 0.1f;
        idleToWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        idleToWalk.AddCondition(AnimatorConditionMode.If,      0f,   "IsGrounded");

        // Walk -> Idle: Speed < 0.1 AND IsGrounded
        var walkToIdle = stateWalk.AddTransition(stateIdle);
        walkToIdle.hasExitTime = false;
        walkToIdle.duration    = 0.1f;
        walkToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        walkToIdle.AddCondition(AnimatorConditionMode.If,   0f,   "IsGrounded");

        // Walk -> Run: Speed > 0.6 AND IsGrounded
        var walkToRun = stateWalk.AddTransition(stateRun);
        walkToRun.hasExitTime = false;
        walkToRun.duration    = 0.1f;
        walkToRun.AddCondition(AnimatorConditionMode.Greater, 0.6f, "Speed");
        walkToRun.AddCondition(AnimatorConditionMode.If,      0f,   "IsGrounded");

        // Run -> Walk: Speed < 0.5 AND IsGrounded (hysteresis)
        var runToWalk = stateRun.AddTransition(stateWalk);
        runToWalk.hasExitTime = false;
        runToWalk.duration    = 0.1f;
        runToWalk.AddCondition(AnimatorConditionMode.Less, 0.5f, "Speed");
        runToWalk.AddCondition(AnimatorConditionMode.If,   0f,   "IsGrounded");

        // Run -> Idle: Speed < 0.1 AND IsGrounded
        var runToIdle = stateRun.AddTransition(stateIdle);
        runToIdle.hasExitTime = false;
        runToIdle.duration    = 0.1f;
        runToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        runToIdle.AddCondition(AnimatorConditionMode.If,   0f,   "IsGrounded");

        // ----- Jump transitions (from Any State) -----

        // Any State -> JumpUp: NOT IsGrounded AND VelocityY > 0.5
        var anyToJumpUp = sm.AddAnyStateTransition(stateJumpUp);
        anyToJumpUp.hasExitTime = false;
        anyToJumpUp.duration    = 0.1f;
        anyToJumpUp.canTransitionToSelf = false;
        anyToJumpUp.AddCondition(AnimatorConditionMode.IfNot,  0f,  "IsGrounded");
        anyToJumpUp.AddCondition(AnimatorConditionMode.Greater, 0.5f, "VelocityY");

        // Any State -> JumpDown: NOT IsGrounded AND VelocityY < -0.5
        // (for portal exits where character starts mid-fall)
        var anyToJumpDown = sm.AddAnyStateTransition(stateJumpDown);
        anyToJumpDown.hasExitTime = false;
        anyToJumpDown.duration    = 0.1f;
        anyToJumpDown.canTransitionToSelf = false;
        anyToJumpDown.AddCondition(AnimatorConditionMode.IfNot, 0f,   "IsGrounded");
        anyToJumpDown.AddCondition(AnimatorConditionMode.Less,  -0.5f, "VelocityY");

        // JumpUp -> JumpDown: VelocityY < 0
        var jumpUpToDown = stateJumpUp.AddTransition(stateJumpDown);
        jumpUpToDown.hasExitTime = false;
        jumpUpToDown.duration    = 0.15f;
        jumpUpToDown.AddCondition(AnimatorConditionMode.Less, 0f, "VelocityY");

        // JumpDown -> Idle: IsGrounded
        var jumpDownToIdle = stateJumpDown.AddTransition(stateIdle);
        jumpDownToIdle.hasExitTime = false;
        jumpDownToIdle.duration    = 0.1f;
        jumpDownToIdle.AddCondition(AnimatorConditionMode.If, 0f, "IsGrounded");

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("[RebuildHazmatController] AnimatorController rebuilt at: " + CONTROLLER_PATH);
    }

    // -------------------------------------------------------------------------
    // Scene Animator Fixer
    // -------------------------------------------------------------------------

    static void FixPlayerAnimator(string playerName, string hazmatModelPath)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CONTROLLER_PATH);
        if (controller == null)
        {
            Debug.LogError("[RebuildHazmatController] Cannot load controller at: " + CONTROLLER_PATH);
            return;
        }

        // Find the HazmatManModel child by path
        var go = GameObject.Find(hazmatModelPath);
        if (go == null)
        {
            // Fallback: search by name under the player
            var player = GameObject.Find(playerName);
            if (player != null)
            {
                var t = player.transform.Find("HazmatManModel");
                if (t != null) go = t.gameObject;
            }
        }

        if (go == null)
        {
            Debug.LogWarning("[RebuildHazmatController] Cannot find HazmatManModel under: " + playerName);
            return;
        }

        var anim = go.GetComponent<Animator>();
        if (anim == null)
        {
            Debug.LogWarning("[RebuildHazmatController] No Animator on: " + go.name + " (path: " + hazmatModelPath + ")");
            return;
        }

        anim.runtimeAnimatorController = controller;
        anim.applyRootMotion = false;

        // Assign the Humanoid avatar from the character FBX
        var charAvatar = LoadCharacterAvatar();
        if (charAvatar != null)
            anim.avatar = charAvatar;

        EditorUtility.SetDirty(go);
        Debug.Log("[RebuildHazmatController] Animator on '" + hazmatModelPath + "' now uses HazmatManController (Humanoid)");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    static ModelImporter GetImporter(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as ModelImporter;
        if (imp == null)
            Debug.LogError("[RebuildHazmatController] Cannot get ModelImporter for: " + path);
        return imp;
    }

    static AnimationClip LoadClip(string fbxPath, string clipName)
    {
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            if (a is AnimationClip c && c.name == clipName)
                return c;
        // Fallback: first non-preview clip (for FBXes with only one clip)
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            if (a is AnimationClip c && !c.name.StartsWith("__preview__"))
            {
                Debug.LogWarning("[RebuildHazmatController] Clip '" + clipName + "' not found in " + fbxPath + "; using fallback: " + c.name);
                return c;
            }
        return null;
    }

    static void LogClip(string label, AnimationClip clip)
    {
        if (clip == null)
            Debug.LogWarning("[RebuildHazmatController] " + label + ": NOT FOUND");
        else
            Debug.Log("[RebuildHazmatController] " + label + ": '" + clip.name + "' " + clip.length.ToString("F2") + "s, loop=" + clip.isLooping);
    }
}
