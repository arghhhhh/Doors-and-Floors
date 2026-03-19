using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using sl;

/// <summary>
/// Editor window that converts a ZED body tracking recording (JSON) to a Humanoid AnimationClip.
/// Uses HumanPoseHandler to compute muscle values from joint positions.
/// Menu: Tools/Mocap to Animation Clip
/// </summary>
public class MocapToAnimClip : EditorWindow
{
    TextAsset recordingFile;
    Animator targetAnimator;
    string clipName = "NewMocapClip";
    bool loopClip = true;

    // ZED BODY_38 → HumanBodyBones mapping
    static readonly Dictionary<int, HumanBodyBones> ZedToHuman = new Dictionary<int, HumanBodyBones>
    {
        { 0,  HumanBodyBones.Hips },
        { 1,  HumanBodyBones.Spine },
        { 2,  HumanBodyBones.Chest },
        { 3,  HumanBodyBones.UpperChest },
        { 4,  HumanBodyBones.Neck },
        { 5,  HumanBodyBones.Head },
        { 10, HumanBodyBones.LeftShoulder },
        { 11, HumanBodyBones.RightShoulder },
        { 12, HumanBodyBones.LeftUpperArm },
        { 13, HumanBodyBones.RightUpperArm },
        { 14, HumanBodyBones.LeftLowerArm },
        { 15, HumanBodyBones.RightLowerArm },
        { 16, HumanBodyBones.LeftHand },
        { 17, HumanBodyBones.RightHand },
        { 18, HumanBodyBones.LeftUpperLeg },
        { 19, HumanBodyBones.RightUpperLeg },
        { 20, HumanBodyBones.LeftLowerLeg },
        { 21, HumanBodyBones.RightLowerLeg },
        { 22, HumanBodyBones.LeftFoot },
        { 23, HumanBodyBones.RightFoot },
        { 24, HumanBodyBones.LeftToes },
        { 25, HumanBodyBones.RightToes },
    };

    // Bone chains: parent joint → child joint (determines bone direction)
    static readonly int[][] BoneChains = new int[][]
    {
        new[] { 0, 1 },   new[] { 1, 2 },   new[] { 2, 3 },
        new[] { 3, 4 },   new[] { 4, 5 },
        new[] { 10, 12 }, new[] { 12, 14 }, new[] { 14, 16 },
        new[] { 11, 13 }, new[] { 13, 15 }, new[] { 15, 17 },
        new[] { 18, 20 }, new[] { 20, 22 }, new[] { 22, 24 },
        new[] { 19, 21 }, new[] { 21, 23 }, new[] { 23, 25 },
    };

    [MenuItem("Tools/Mocap to Animation Clip")]
    static void OpenWindow()
    {
        GetWindow<MocapToAnimClip>("Mocap → Clip");
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("ZED Mocap → Humanoid Animation Clip", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        recordingFile = (TextAsset)EditorGUILayout.ObjectField("Recording JSON", recordingFile, typeof(TextAsset), false);
        targetAnimator = (Animator)EditorGUILayout.ObjectField("Target Animator (Humanoid)", targetAnimator, typeof(Animator), true);
        clipName = EditorGUILayout.TextField("Clip Name", clipName);
        loopClip = EditorGUILayout.Toggle("Loop", loopClip);

        EditorGUILayout.Space();

        bool valid = recordingFile != null && targetAnimator != null;
        if (valid && !targetAnimator.isHuman)
        {
            EditorGUILayout.HelpBox("Animator must use a Humanoid avatar!", MessageType.Error);
            valid = false;
        }

        GUI.enabled = valid;
        if (GUILayout.Button("Generate Animation Clip", GUILayout.Height(30)))
        {
            GenerateClip();
        }
        GUI.enabled = true;

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "1. Record a gesture in Play Mode (press R)\n" +
            "2. Drag the JSON from Assets/Recordings here\n" +
            "3. Drag the Player's Animator (Humanoid) here\n" +
            "4. Click Generate",
            MessageType.Info);
    }

    void GenerateClip()
    {
        var recording = JsonUtility.FromJson<BodyTrackingRecorder.RecordingData>(recordingFile.text);
        if (recording == null || recording.frames.Count == 0)
        {
            EditorUtility.DisplayDialog("Error", "Recording is empty or invalid.", "OK");
            return;
        }

        Avatar avatar = targetAnimator.avatar;

        // Create a temporary instance to manipulate bones
        GameObject tempGo = Instantiate(targetAnimator.gameObject);
        tempGo.name = "TempMocapAvatar";
        tempGo.hideFlags = HideFlags.HideAndDontSave;
        Animator tempAnim = tempGo.GetComponent<Animator>();

        HumanPoseHandler poseHandler = new HumanPoseHandler(avatar, tempGo.transform);
        HumanPose humanPose = new HumanPose();

        // Get bind pose bone directions (in world space)
        Dictionary<int, Vector3> bindBoneDirections = new Dictionary<int, Vector3>();
        foreach (var chain in BoneChains)
        {
            int parentIdx = chain[0];
            int childIdx = chain[1];

            if (!ZedToHuman.ContainsKey(parentIdx) || !ZedToHuman.ContainsKey(childIdx)) continue;

            Transform parentBone = tempAnim.GetBoneTransform(ZedToHuman[parentIdx]);
            Transform childBone = tempAnim.GetBoneTransform(ZedToHuman[childIdx]);

            if (parentBone != null && childBone != null)
            {
                Vector3 dir = (childBone.position - parentBone.position).normalized;
                if (dir.sqrMagnitude > 0.001f)
                    bindBoneDirections[parentIdx] = dir;
            }
        }

        // Store bind pose rotations
        Dictionary<int, Quaternion> bindRotations = new Dictionary<int, Quaternion>();
        foreach (var kvp in ZedToHuman)
        {
            Transform bone = tempAnim.GetBoneTransform(kvp.Value);
            if (bone != null)
                bindRotations[kvp.Key] = bone.rotation;
        }

        int muscleCount = HumanTrait.MuscleCount;
        // Muscle curves + body position (3) + body rotation (4)
        List<Keyframe>[] muscleCurves = new List<Keyframe>[muscleCount];
        for (int i = 0; i < muscleCount; i++)
            muscleCurves[i] = new List<Keyframe>();

        List<Keyframe>[] bodyPosCurves = new List<Keyframe>[3];
        List<Keyframe>[] bodyRotCurves = new List<Keyframe>[4];
        for (int i = 0; i < 3; i++) bodyPosCurves[i] = new List<Keyframe>();
        for (int i = 0; i < 4; i++) bodyRotCurves[i] = new List<Keyframe>();

        // Use floor plane data for gravity alignment if available
        Quaternion gravityCorrection = Quaternion.identity;
        float floorHeight = 0f; // Y position of the floor in corrected space
        bool hasFloorPlane = recording.floorPlane != null && recording.floorPlane.detected;

        if (hasFloorPlane)
        {
            gravityCorrection = new Quaternion(
                recording.floorPlane.rotX, recording.floorPlane.rotY,
                recording.floorPlane.rotZ, recording.floorPlane.rotW);

            // Floor center in gravity-corrected space gives us the ground Y
            Vector3 floorCenter = new Vector3(
                recording.floorPlane.centerX,
                recording.floorPlane.centerY,
                recording.floorPlane.centerZ);
            Vector3 correctedFloorCenter = gravityCorrection * floorCenter;
            floorHeight = correctedFloorCenter.y;

            Debug.Log($"[MocapToAnimClip] Gravity correction: {gravityCorrection.eulerAngles}, floorY={floorHeight:F3}");

            Debug.Log($"[MocapToAnimClip] Using floor plane: playerHeight={recording.floorPlane.playerHeight:F2}, floorY={floorHeight:F2}");
        }
        else
        {
            Debug.LogWarning("[MocapToAnimClip] No floor plane in recording. Using spine-based fallback.");
            // Fallback: estimate from first frame spine direction
            var ff = recording.frames[0];
            Vector3 pelvis = new Vector3(-ff.keypointsFlat[0], ff.keypointsFlat[1], -ff.keypointsFlat[2]);
            Vector3 neck = new Vector3(-ff.keypointsFlat[4 * 3], ff.keypointsFlat[4 * 3 + 1], -ff.keypointsFlat[4 * 3 + 2]);
            Vector3 spineDir = (neck - pelvis).normalized;
            gravityCorrection = Quaternion.FromToRotation(spineDir, Vector3.up);
        }

        // 180° Y rotation to face the avatar forward (instead of mirror which breaks handedness)
        Quaternion facingFlip = Quaternion.Euler(0f, 180f, 0f);

        // Reference pelvis position from first frame (gravity-corrected, then rotated)
        Vector3 firstPelvisRaw = new Vector3(
            recording.frames[0].keypointsFlat[0],
            recording.frames[0].keypointsFlat[1],
            recording.frames[0].keypointsFlat[2]
        );
        Vector3 firstPelvis = facingFlip * (gravityCorrection * firstPelvisRaw);

        // Process each frame
        for (int f = 0; f < recording.frames.Count; f++)
        {
            var frame = recording.frames[f];
            float time = frame.time;

            // Reconstruct keypoints: gravity-correct, then rotate 180° to face forward
            Vector3[] keypoints = new Vector3[38];
            for (int i = 0; i < 38; i++)
            {
                Vector3 raw = new Vector3(
                    frame.keypointsFlat[i * 3 + 0],
                    frame.keypointsFlat[i * 3 + 1],
                    frame.keypointsFlat[i * 3 + 2]
                );
                keypoints[i] = facingFlip * (gravityCorrection * raw);
            }

            // Reset temp skeleton to bind pose
            foreach (var kvp in bindRotations)
            {
                if (!ZedToHuman.ContainsKey(kvp.Key)) continue;
                Transform bone = tempAnim.GetBoneTransform(ZedToHuman[kvp.Key]);
                if (bone != null)
                    bone.rotation = kvp.Value;
            }

            // Apply captured rotations by computing delta from bind pose directions
            foreach (var chain in BoneChains)
            {
                int parentIdx = chain[0];
                int childIdx = chain[1];

                if (!ZedToHuman.ContainsKey(parentIdx)) continue;
                if (!bindBoneDirections.ContainsKey(parentIdx)) continue;
                if (!bindRotations.ContainsKey(parentIdx)) continue;

                Vector3 capturedDir = (keypoints[childIdx] - keypoints[parentIdx]).normalized;
                if (capturedDir.sqrMagnitude < 0.001f) continue;

                Vector3 bindDir = bindBoneDirections[parentIdx];

                // Delta rotation from bind direction to captured direction
                Quaternion delta = Quaternion.FromToRotation(bindDir, capturedDir);

                Transform bone = tempAnim.GetBoneTransform(ZedToHuman[parentIdx]);
                if (bone != null)
                    bone.rotation = delta * bindRotations[parentIdx];
            }

            // Position hips: use floor plane for ground reference
            Transform hipsBone = tempAnim.GetBoneTransform(HumanBodyBones.Hips);
            if (hipsBone != null)
            {
                // Center X/Z on first frame, use corrected Y relative to floor
                Vector3 hipsOffset = keypoints[0] - firstPelvis;
                float hipsAboveFloor = keypoints[0].y - floorHeight;
                hipsBone.position = tempGo.transform.position + new Vector3(hipsOffset.x, hipsAboveFloor, hipsOffset.z);
            }

            // Read back the HumanPose (muscle values)
            poseHandler.GetHumanPose(ref humanPose);

            // Record body position and rotation
            bodyPosCurves[0].Add(new Keyframe(time, humanPose.bodyPosition.x));
            bodyPosCurves[1].Add(new Keyframe(time, humanPose.bodyPosition.y));
            bodyPosCurves[2].Add(new Keyframe(time, humanPose.bodyPosition.z));

            bodyRotCurves[0].Add(new Keyframe(time, humanPose.bodyRotation.x));
            bodyRotCurves[1].Add(new Keyframe(time, humanPose.bodyRotation.y));
            bodyRotCurves[2].Add(new Keyframe(time, humanPose.bodyRotation.z));
            bodyRotCurves[3].Add(new Keyframe(time, humanPose.bodyRotation.w));

            // Record muscle values
            for (int m = 0; m < muscleCount && m < humanPose.muscles.Length; m++)
            {
                muscleCurves[m].Add(new Keyframe(time, humanPose.muscles[m]));
            }
        }

        // Build the AnimationClip
        AnimationClip clip = new AnimationClip();
        clip.frameRate = 30;

        // Body position curves
        string[] bodyPosProps = { "RootT.x", "RootT.y", "RootT.z" };
        for (int i = 0; i < 3; i++)
        {
            if (bodyPosCurves[i].Count > 0)
                clip.SetCurve("", typeof(Animator), bodyPosProps[i], new AnimationCurve(bodyPosCurves[i].ToArray()));
        }

        // Body rotation curves
        string[] bodyRotProps = { "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" };
        for (int i = 0; i < 4; i++)
        {
            if (bodyRotCurves[i].Count > 0)
                clip.SetCurve("", typeof(Animator), bodyRotProps[i], new AnimationCurve(bodyRotCurves[i].ToArray()));
        }

        // Muscle curves
        for (int m = 0; m < muscleCount; m++)
        {
            if (muscleCurves[m].Count == 0) continue;

            string muscleName = HumanTrait.MuscleName[m];
            clip.SetCurve("", typeof(Animator), muscleName, new AnimationCurve(muscleCurves[m].ToArray()));
        }

        // Set loop
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loopClip;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        // Cleanup temp object
        poseHandler.Dispose();
        DestroyImmediate(tempGo);

        // Save the clip
        string outputDir = "Assets/Animation/Mocap";
        if (!AssetDatabase.IsValidFolder(outputDir))
            AssetDatabase.CreateFolder("Assets/Animation", "Mocap");

        string assetPath = $"{outputDir}/{clipName}.anim";
        assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
        AssetDatabase.CreateAsset(clip, assetPath);
        AssetDatabase.SaveAssets();

        Debug.Log($"[MocapToAnimClip] Saved Humanoid clip: {assetPath} ({recording.frames.Count} frames, {muscleCount} muscles)");

        EditorUtility.DisplayDialog("Success",
            $"Humanoid animation clip saved to:\n{assetPath}\n\n" +
            $"Frames: {recording.frames.Count}\n" +
            $"Duration: {recording.frames[recording.frames.Count - 1].time:F1}s\n" +
            $"Muscles: {muscleCount}",
            "OK");

        Selection.activeObject = clip;
        EditorGUIUtility.PingObject(clip);
    }
}
