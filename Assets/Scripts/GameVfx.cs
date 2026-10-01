using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// Plays the gameplay VFX Graph effects (jump dust, portal in/out, win confetti).
/// Every effect is an event-triggered burst in world space, so one VisualEffect can fire
/// at any position. Jump and portal effects get one instance per player: a VisualEffect
/// merges two SendEvent calls in the same frame into one burst, and both players can jump
/// or teleport on the same frame.
///
/// Effects must stay on a layer the pixel pass draws (BasicFeature) and use opaque
/// outputs: the main renderer only draws UI/NoPixel itself, and the pixel pass only draws
/// opaque queues, so transparent particles would be invisible.
/// </summary>
public class GameVfx : MonoBehaviour
{
    public static GameVfx Instance { get; private set; }

    [Header("Effects")]
    public VisualEffectAsset jumpDust;
    public VisualEffectAsset portalIn;
    public VisualEffectAsset portalOut;
    public VisualEffectAsset confettiRain;
    public VisualEffectAsset confettiBurst;

    [Header("Placement")]
    [Tooltip("Z for portal effects: in front of the doors (z = 0) and the players (z = -0.3)")]
    public float portalEffectZ = -0.5f;

    static readonly int PositionId = Shader.PropertyToID("position");
    static readonly int ColorId = Shader.PropertyToID("color");

    const string JumpEvent = "OnJump";
    const string PortalInEvent = "OnPortalIn";
    const string PortalOutEvent = "OnPortalOut";
    const string WinEvent = "OnWin";
    const string WinBurstEvent = "OnWinBurst";

    // An event attribute is created by (and only valid for) one VisualEffect, so each
    // instance keeps its own
    readonly Dictionary<(int player, VisualEffectAsset asset), (VisualEffect vfx, VFXEventAttribute attr)> instances =
        new Dictionary<(int, VisualEffectAsset), (VisualEffect, VFXEventAttribute)>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void PlayJump(PlayerController player)
    {
        Fire(player.playerNumber, jumpDust, JumpEvent, player.transform.position, Color.white);
    }

    /// <summary>Ring collapsing into the door the player is entering.</summary>
    public void PlayPortalIn(PlayerController player, Vector3 doorPosition, Color doorColor)
    {
        doorPosition.z = portalEffectZ;
        Fire(player.playerNumber, portalIn, PortalInEvent, doorPosition, doorColor);
    }

    /// <summary>Burst out of the door the player arrives at.</summary>
    public void PlayPortalOut(PlayerController player, Vector3 exitPosition, Color doorColor)
    {
        exitPosition.z = portalEffectZ;
        Fire(player.playerNumber, portalOut, PortalOutEvent, exitPosition, doorColor);
    }

    /// <summary>Confetti over the whole building plus a fountain from the winner.</summary>
    public void PlayWin(PlayerController winner)
    {
        Fire(0, confettiRain, WinEvent, Vector3.zero, Color.white);
        if (winner != null)
            Fire(0, confettiBurst, WinBurstEvent, winner.transform.position, Color.white);
    }

    /// <summary>Removes every live particle (restart reuses the scene).</summary>
    public void ClearAll()
    {
        foreach (var entry in instances.Values)
            if (entry.vfx != null) entry.vfx.Reinit();
    }

    void Fire(int playerNumber, VisualEffectAsset asset, string eventName, Vector3 position, Color color)
    {
        if (asset == null) return;

        var (vfx, attr) = GetInstance(playerNumber, asset);

        attr.SetVector3(PositionId, position);
        // VFX colour attributes are linear; door colours are authored in sRGB
        Color linear = color.linear;
        attr.SetVector3(ColorId, new Vector3(linear.r, linear.g, linear.b));
        vfx.SendEvent(eventName, attr);
    }

    (VisualEffect, VFXEventAttribute) GetInstance(int playerNumber, VisualEffectAsset asset)
    {
        var key = (playerNumber, asset);
        if (instances.TryGetValue(key, out var existing) && existing.vfx != null)
            return existing;

        var go = new GameObject($"{asset.name}_P{playerNumber}");
        go.transform.SetParent(transform, false);
        // Inherit this object's layer so the pixel pass draws it
        go.layer = gameObject.layer;
        VisualEffect vfx = go.AddComponent<VisualEffect>();
        vfx.visualEffectAsset = asset;
        var entry = (vfx, vfx.CreateVFXEventAttribute());
        instances[key] = entry;
        return entry;
    }
}
