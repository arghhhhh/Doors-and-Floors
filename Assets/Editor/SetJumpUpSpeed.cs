using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class SetJumpUpSpeed
{
    [MenuItem("Tools/Set JumpUp Speed")]
    public static void Execute()
    {
        const string controllerPath = "Assets/Animation/HazmatManController.controller";
        const string targetState    = "JumpUp";
        const float  targetSpeed    = 1.5f;

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            Debug.LogError($"[SetJumpUpSpeed] AnimatorController not found at '{controllerPath}'.");
            return;
        }

        bool found = false;
        foreach (var layer in controller.layers)
        {
            foreach (var state in layer.stateMachine.states)
            {
                if (state.state.name == targetState)
                {
                    state.state.speed = targetSpeed;
                    EditorUtility.SetDirty(state.state);
                    found = true;
                    Debug.Log($"[SetJumpUpSpeed] Set '{targetState}' speed to {targetSpeed} in layer '{layer.name}'.");
                }
            }
        }

        if (!found)
        {
            Debug.LogWarning($"[SetJumpUpSpeed] State '{targetState}' was not found in any layer of '{controllerPath}'.");
            return;
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("[SetJumpUpSpeed] Asset saved.");
    }
}
 
