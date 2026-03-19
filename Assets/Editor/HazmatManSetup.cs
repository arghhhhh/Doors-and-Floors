using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;

/// <summary>
/// One-shot editor tool to set up the Hazmat Man character in the GameScreen scene.
/// Run via: Tools > Setup Hazmat Man
/// </summary>
public static class HazmatManSetup
{
    const string MODEL_BASE = "Assets/Models/Meshy_AI_hazmat_man_1_biped_separate/Meshy_AI_hazmat_man_1_biped/";
    const string CHARACTER_FBX = MODEL_BASE + "Meshy_AI_hazmat_man_1_biped_Character_output.fbx";
    const string ANIM_RUNNING  = MODEL_BASE + "Meshy_AI_hazmat_man_1_biped_Animation_Running_withSkin.fbx";
    const string ANIM_WALK_IP  = MODEL_BASE + "Meshy_AI_hazmat_man_1_biped_Animation_walking_2_inplace_withSkin.fbx";
    const string ANIM_JUMP     = MODEL_BASE + "Meshy_AI_hazmat_man_1_biped_Animation_Regular_Jump_withSkin.fbx";
    const string ANIM_FALL     = MODEL_BASE + "Meshy_AI_hazmat_man_1_biped_Animation_Jumping_Down_withSkin.fbx";

    const string TEXTURE_ALBEDO    = MODEL_BASE + "Meshy_AI_hazmat_man_1_biped_texture_0.png";
    const string TEXTURE_METALLIC  = MODEL_BASE + "Meshy_AI_hazmat_man_1_biped_texture_0_metallic.png";
    const string TEXTURE_NORMAL    = MODEL_BASE + "Meshy_AI_hazmat_man_1_biped_texture_0_normal.png";
    const string TEXTURE_ROUGHNESS = MODEL_BASE + "Meshy_AI_hazmat_man_1_biped_texture_0_roughness.png";

    const string MAT_PATH        = "Assets/Models/Meshy_AI_hazmat_man_1_biped_separate/HazmatManMaterial.mat";
    const string CONTROLLER_PATH = "Assets/Animation/HazmatManController.controller";

    [MenuItem("Tools/Setup Hazmat Man")]
    public static void Run()
    {
        // â”€â”€ Step 1: Ensure Animation folder â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        if (!AssetDatabase.IsValidFolder("Assets/Animation"))
            AssetDatabase.CreateFolder("Assets", "Animation");

        // â”€â”€ Step 2: Create / update URP material â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        Material mat = CreateOrUpdateMaterial();

        // â”€â”€ Step 3: Create / update AnimatorController â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        AnimatorController controller = CreateOrUpdateController();

        // â”€â”€ Step 4: Swap Player1 and Player2 in the active scene â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        SwapPlayer("Player1", mat, controller);
        SwapPlayer("Player2", mat, controller);

        // â”€â”€ Step 5: Save â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        Debug.Log("[HazmatManSetup] Done! Both players now use the Hazmat Man character.");
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Material
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    static Material CreateOrUpdateMaterial()
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MAT_PATH);
        if (mat == null)
        {
            // URP Lit shader
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, MAT_PATH);
        }

        var albedo   = AssetDatabase.LoadAssetAtPath<Texture2D>(TEXTURE_ALBEDO);
        var metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(TEXTURE_METALLIC);
        var normal   = AssetDatabase.LoadAssetAtPath<Texture2D>(TEXTURE_NORMAL);

        if (albedo   != null) mat.SetTexture("_BaseMap",     albedo);
        if (albedo   != null) mat.SetTexture("_MainTex",     albedo);   // fallback for Standard
        if (metallic != null) mat.SetTexture("_MetallicGlossMap", metallic);
        if (normal   != null) mat.SetTexture("_BumpMap",     normal);
        if (normal   != null) mat.EnableKeyword("_NORMALMAP");

        // Roughness -> set smoothness = 0 and use roughness texture as smoothness source
        // URP stores smoothness in the alpha of the metallic map; we just lower the
        // smoothness slider since roughness = 1 - smoothness and this is a hazmat suit.
        mat.SetFloat("_Smoothness", 0.2f);
        mat.SetFloat("_Glossiness", 0.2f);

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        Debug.Log($"[HazmatManSetup] Material saved: {MAT_PATH}");
        return mat;
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // AnimatorController
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    static AnimatorController CreateOrUpdateController()
    {
        // Load animation clips from the FBX files
        AnimationClip clipIdle    = LoadFirstClip(ANIM_WALK_IP);
        AnimationClip clipRun     = LoadFirstClip(ANIM_RUNNING);
        AnimationClip clipJump    = LoadFirstClip(ANIM_JUMP);
        AnimationClip clipFall    = LoadFirstClip(ANIM_FALL);

        if (clipIdle == null) Debug.LogWarning("[HazmatManSetup] Could not load idle clip from: " + ANIM_WALK_IP);
        if (clipRun  == null) Debug.LogWarning("[HazmatManSetup] Could not load run clip from: "  + ANIM_RUNNING);
        if (clipJump == null) Debug.LogWarning("[HazmatManSetup] Could not load jump clip from: " + ANIM_JUMP);
        if (clipFall == null) Debug.LogWarning("[HazmatManSetup] Could not load fall clip from: " + ANIM_FALL);

        // Create controller
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(CONTROLLER_PATH);

        // Parameters
        controller.AddParameter("Speed",      AnimatorControllerParameterType.Float);
        controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsJumping",  AnimatorControllerParameterType.Bool);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;

        // States
        AnimatorState stateIdle = sm.AddState("Idle");
        AnimatorState stateRun  = sm.AddState("Run");
        AnimatorState stateJump = sm.AddState("Jump");
        AnimatorState stateFall = sm.AddState("Fall");

        if (clipIdle != null) stateIdle.motion = clipIdle;
        if (clipRun  != null) stateRun.motion  = clipRun;
        if (clipJump != null) stateJump.motion  = clipJump;
        if (clipFall != null) stateFall.motion  = clipFall;

        // Default state = Idle
        sm.defaultState = stateIdle;

        // Transitions â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        // Idle -> Run: Speed > 0.1
        var idleToRun = stateIdle.AddTransition(stateRun);
        idleToRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        idleToRun.hasExitTime = false;
        idleToRun.duration = 0.1f;

        // Run -> Idle: Speed < 0.1
        var runToIdle = stateRun.AddTransition(stateIdle);
        runToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        runToIdle.hasExitTime = false;
        runToIdle.duration = 0.1f;

        // Idle -> Jump: IsJumping = true
        var idleToJump = stateIdle.AddTransition(stateJump);
        idleToJump.AddCondition(AnimatorConditionMode.If, 0, "IsJumping");
        idleToJump.hasExitTime = false;
        idleToJump.duration = 0.05f;

        // Run -> Jump: IsJumping = true
        var runToJump = stateRun.AddTransition(stateJump);
        runToJump.AddCondition(AnimatorConditionMode.If, 0, "IsJumping");
        runToJump.hasExitTime = false;
        runToJump.duration = 0.05f;

        // Jump -> Fall: exit time (after jump clip finishes)
        var jumpToFall = stateJump.AddTransition(stateFall);
        jumpToFall.hasExitTime = true;
        jumpToFall.exitTime = 0.9f;
        jumpToFall.duration = 0.1f;

        // Jump -> Fall (early): IsGrounded = false after clip starts
        var jumpToFallEarly = stateJump.AddTransition(stateFall);
        jumpToFallEarly.AddCondition(AnimatorConditionMode.IfNot, 0, "IsGrounded");
        jumpToFallEarly.hasExitTime = true;
        jumpToFallEarly.exitTime = 0.5f;
        jumpToFallEarly.duration = 0.1f;

        // Fall -> Idle: IsGrounded = true
        var fallToIdle = stateFall.AddTransition(stateIdle);
        fallToIdle.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");
        fallToIdle.hasExitTime = false;
        fallToIdle.duration = 0.1f;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log($"[HazmatManSetup] AnimatorController saved: {CONTROLLER_PATH}");
        return controller;
    }

    static AnimationClip LoadFirstClip(string fbxPath)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        foreach (Object a in assets)
            if (a is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                return clip;
        return null;
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Scene swap
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    static void SwapPlayer(string playerName, Material mat, AnimatorController controller)
    {
        GameObject playerGO = GameObject.Find(playerName);
        if (playerGO == null)
        {
            Debug.LogWarning($"[HazmatManSetup] Could not find '{playerName}' in the active scene.");
            return;
        }

        // Remove existing capsule visuals
        MeshFilter mf = playerGO.GetComponent<MeshFilter>();
        MeshRenderer mr = playerGO.GetComponent<MeshRenderer>();
        if (mr != null) Object.DestroyImmediate(mr);
        if (mf != null) Object.DestroyImmediate(mf);

        // Remove any previous character model child
        Transform existing = playerGO.transform.Find("HazmatManModel");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        // Instantiate character model as child
        GameObject characterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CHARACTER_FBX);
        if (characterPrefab == null)
        {
            Debug.LogError($"[HazmatManSetup] Could not load character FBX: {CHARACTER_FBX}");
            return;
        }

        GameObject modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(characterPrefab, playerGO.transform);
        modelInstance.name = "HazmatManModel";

        // The player root is scaled (0.34, 0.34, 0.34). The character FBX is imported at scale 1.
        // We want the final world scale to be roughly 1, so we invert the parent scale.
        // A hazmat man standing ~1.8m tall: localScale â‰ˆ (1/0.34) â‰ˆ 2.94
        float inverseScale = 1f / 0.34f;
        modelInstance.transform.localScale    = new Vector3(inverseScale, inverseScale, inverseScale);
        modelInstance.transform.localPosition = Vector3.zero;
        modelInstance.transform.localRotation = Quaternion.identity;

        // Apply material to all SkinnedMeshRenderers in the model
        foreach (SkinnedMeshRenderer smr in modelInstance.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            Material[] mats = new Material[smr.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            smr.sharedMaterials = mats;
        }
        // Also apply to any regular MeshRenderers in the model
        foreach (MeshRenderer r in modelInstance.GetComponentsInChildren<MeshRenderer>())
        {
            Material[] mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            r.sharedMaterials = mats;
        }

        // Add Animator on root player GameObject (not on the model child, so PlayerController can find it easily)
        Animator anim = playerGO.GetComponent<Animator>();
        if (anim == null) anim = playerGO.AddComponent<Animator>();
        anim.runtimeAnimatorController = controller;
        anim.applyRootMotion = false;  // we drive movement via Rigidbody

        // Adjust CapsuleCollider to better fit the character
        // The player scale is (0.34, 0.34, 0.34) so unscaled units are approx 5.88 for 2m world height.
        // Current: height=2, radius=0.5 which gives world height = 2*0.34 = 0.68m â€” too short.
        // Set height=5.5, radius=1.5 so world height â‰ˆ 1.87m, radius â‰ˆ 0.51m
        CapsuleCollider cap = playerGO.GetComponent<CapsuleCollider>();
        if (cap != null)
        {
            cap.height = 5.5f;
            cap.radius = 1.5f;
            cap.center = new Vector3(0f, 2.75f, 0f); // bottom at y=0, center at y=half-height
        }

        EditorUtility.SetDirty(playerGO);
        Debug.Log($"[HazmatManSetup] Swapped '{playerName}' to use Hazmat Man model.");
    }
}
 
