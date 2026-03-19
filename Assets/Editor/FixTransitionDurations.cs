using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using System.Collections.Generic;

public static class FixTransitionDurations
{
    private const string ControllerPath = "Assets/Animation/HazmatManController.controller";
    private const float JumpTransitionDuration = 0.05f;
    private const float DefaultTransitionDuration = 0.1f;

    [MenuItem("Tools/Fix Transition Durations")]
    public static void Fix()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"[FixTransitionDurations] Could not load AnimatorController at {ControllerPath}");
            return;
        }

        int jumpFixed = 0;
        int otherFixed = 0;

        foreach (var layer in controller.layers)
        {
            ProcessStateMachine(layer.stateMachine, ref jumpFixed, ref otherFixed);
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        Debug.Log($"[FixTransitionDurations] Done. Jump transitions fixed: {jumpFixed}, Other transitions fixed: {otherFixed}");
    }

    private static void ProcessStateMachine(AnimatorStateMachine sm, ref int jumpFixed, ref int otherFixed)
    {
        // Build a lookup of fileID -> state name so we can check destination state names.
        // We walk all states in this SM (and sub-SMs) to collect names by object reference.
        var stateNames = new Dictionary<AnimatorState, string>();
        CollectStateNames(sm, stateNames);

        // Process transitions on every state in this layer
        foreach (var childState in sm.states)
        {
            var state = childState.state;
            bool sourceIsJump = IsJumpState(state.name);

            foreach (var transition in state.transitions)
            {
                bool destIsJump = transition.destinationState != null && IsJumpState(transition.destinationState.name);
                ApplyDuration(transition, sourceIsJump || destIsJump, ref jumpFixed, ref otherFixed);
            }
        }

        // Any State transitions
        foreach (var transition in sm.anyStateTransitions)
        {
            bool destIsJump = transition.destinationState != null && IsJumpState(transition.destinationState.name);
            // Any State is never itself JumpUp/JumpDown, so only check destination
            ApplyDuration(transition, destIsJump, ref jumpFixed, ref otherFixed);
        }

        // Entry transitions (AnimatorTransition, not AnimatorStateTransition — no duration field)
        // These don't have a duration property, so we skip them intentionally.

        // Recurse into sub-state machines
        foreach (var childSM in sm.stateMachines)
        {
            ProcessStateMachine(childSM.stateMachine, ref jumpFixed, ref otherFixed);
        }
    }

    private static void CollectStateNames(AnimatorStateMachine sm, Dictionary<AnimatorState, string> map)
    {
        foreach (var cs in sm.states)
            map[cs.state] = cs.state.name;

        foreach (var csm in sm.stateMachines)
            CollectStateNames(csm.stateMachine, map);
    }

    private static bool IsJumpState(string stateName)
    {
        return stateName == "JumpUp" || stateName == "JumpDown";
    }

    private static void ApplyDuration(AnimatorStateTransition transition, bool isJump, ref int jumpFixed, ref int otherFixed)
    {
        float target = isJump ? JumpTransitionDuration : DefaultTransitionDuration;
        // hasExitTime is intentionally left unchanged
        transition.duration = target;

        if (isJump)
            jumpFixed++;
        else
            otherFixed++;
    }
}
 
