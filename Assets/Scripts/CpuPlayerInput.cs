using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AI input adapter that races a player to the top. Each time it lands it picks a door on
/// the belt above (preferring the fewest teleports left to the win floor), runs under it
/// tracking the conveyor, and jumps. Difficulty comes from the fields below; use the
/// component's context menu for Easy/Normal/Hard presets.
/// Sits disabled on Player2; GameScreenManager enables it in vs-CPU mode.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class CpuPlayerInput : MonoBehaviour
{
    public enum Difficulty { Easy, Normal, Hard }

    [Header("Speed")]
    [Tooltip("Fraction of PlayerController.moveSpeed the CPU runs at")]
    [Range(0.3f, 1f)]
    public float speedMultiplier = 0.5f;
    [Tooltip("How hard the CPU steers toward its target X (higher = snappier)")]
    public float steeringGain = 6f;

    [Header("Reactions (seconds, random between x and y)")]
    [Tooltip("Pause after landing (and at the start) before choosing a door and moving")]
    public Vector2 reactionTimeRange = new Vector2(1.2f, 2.0f);
    [Tooltip("Pause once lined up under a door before jumping")]
    public Vector2 jumpHesitationRange = new Vector2(0.3f, 0.7f);
    public float jumpCooldown = 0.6f;

    [Header("Mistakes")]
    [Tooltip("Chance of picking a random door that doesn't go down instead of the best one (wastes time without throwing the CPU back to a lower floor)")]
    [Range(0f, 1f)]
    public float wrongDoorChance = 0.3f;
    [Tooltip("Chance a jump is deliberately lined up too far off the door to connect")]
    [Range(0f, 1f)]
    public float missChance = 0.3f;
    [Tooltip("Random X offset (units) applied to every aim, for a less robotic look")]
    public float aimJitter = 0.15f;

    [Header("Fairness")]
    [Tooltip("Stand still while a human player is frozen for tracking loss. The CPU always stops once no human is left in the game.")]
    public bool pauseWhileOpponentTrackingLost = true;

    [Header("Layout")]
    [Tooltip("Furthest |X| the CPU will try to reach (doors past this are partly behind the walls)")]
    public float reachableHalfWidth = 6.2f;
    [Tooltip("How close to the aim point (units) counts as lined up")]
    public float alignTolerance = 0.2f;

    PlayerController pc;
    CapsuleCollider capsule;
    DoorPairGenerator generator;
    readonly List<PlayerController> opponents = new List<PlayerController>();

    PortalDoor targetDoor;
    ConveyorBelt targetBelt;
    float aimOffset;
    float reactionTimer;
    float hesitationTimer;
    float cooldownTimer;
    bool wasGrounded;
    bool aligned;

    const int Unreachable = 1000;

    void Awake()
    {
        pc = GetComponent<PlayerController>();
        capsule = GetComponent<CapsuleCollider>();
    }

    void OnEnable()
    {
        pc.OnReset += ResetState;

        opponents.Clear();
        foreach (var p in FindObjectsOfType<PlayerController>(true))
            if (p != pc) opponents.Add(p);

        ResetState();
    }

    void OnDisable()
    {
        pc.OnReset -= ResetState;
    }

    void ResetState()
    {
        targetDoor = null;
        targetBelt = null;
        aligned = false;
        wasGrounded = false;
        cooldownTimer = 0f;
        // Don't bolt the instant the race starts
        reactionTimer = RandomIn(reactionTimeRange);
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.Playing)
            return;
        if (pc.IsFrozen) return;

        if (ShouldWaitForHumans())
        {
            pc.SetHorizontalVelocity(0f);
            return;
        }

        if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;

        bool grounded = pc.IsGrounded;
        if (grounded && !wasGrounded)
        {
            // Landed (after a teleport, a missed jump, or at spawn): think, then pick again
            reactionTimer = Mathf.Max(reactionTimer, RandomIn(reactionTimeRange));
            targetDoor = null;
        }
        wasGrounded = grounded;

        if (reactionTimer > 0f)
        {
            reactionTimer -= Time.deltaTime;
            pc.SetHorizontalVelocity(0f);
            return;
        }

        // Door destroyed by a regenerate, or drifted out of reach while we were waiting
        if (targetDoor == null || (grounded && !IsReachable(targetDoor.transform.position.x)))
            ChooseDoor();

        if (targetDoor == null)
        {
            pc.SetHorizontalVelocity(0f);
            return;
        }

        float x = transform.position.x;
        float doorX = targetDoor.transform.position.x;
        float aimX = Mathf.Clamp(doorX + aimOffset, -reachableHalfWidth, reachableHalfWidth);

        // Feed-forward the belt speed so we keep pace with the door, then close the gap
        float maxSpeed = pc.moveSpeed * speedMultiplier;
        float vx = BeltVelocity(targetBelt) + (aimX - x) * steeringGain;
        pc.SetHorizontalVelocity(Mathf.Clamp(vx, -maxSpeed, maxSpeed));

        if (!grounded) return;

        if (Mathf.Abs(aimX - x) <= alignTolerance && IsReachable(doorX))
        {
            if (!aligned)
            {
                aligned = true;
                hesitationTimer = RandomIn(jumpHesitationRange);
            }

            hesitationTimer -= Time.deltaTime;
            if (hesitationTimer <= 0f && cooldownTimer <= 0f && pc.TryJump())
            {
                cooldownTimer = jumpCooldown;
                aligned = false;
            }
        }
        else
        {
            aligned = false;
        }
    }

    // ── Door choice ─────────────────────────────────────────────────

    void ChooseDoor()
    {
        targetDoor = null;
        targetBelt = null;
        aligned = false;

        if (generator == null && GameManager.Instance != null)
            generator = GameManager.Instance.doorGenerator;
        if (generator == null || generator.floors == null) return;

        int floor = CurrentFloor();
        if (floor < 0 || floor >= generator.BeltCount) return;

        var candidates = new List<PortalDoor>();
        foreach (var door in generator.GetDoorsOnBelt(floor))
        {
            // Skip destroyed doors and doors that just loop back to this floor
            if (door == null || door.pairedDoor == null) continue;
            if (door.pairedDoor.beltIndex == floor) continue;
            candidates.Add(door);
        }
        if (candidates.Count == 0) return;

        var noDrop = candidates.FindAll(d => d.pairedDoor.beltIndex > floor);
        if (noDrop.Count > 0 && Random.value < wrongDoorChance)
        {
            targetDoor = noDrop[Random.Range(0, noDrop.Count)];
        }
        else
        {
            int[] hops = ComputeHopsToWin();
            float maxSpeed = Mathf.Max(0.1f, pc.moveSpeed * speedMultiplier);
            float bestCost = float.MaxValue;
            foreach (var door in candidates)
            {
                float doorX = door.transform.position.x;
                // Fewer teleports left dominates; travel time and reachability break ties
                float cost = hops[door.pairedDoor.beltIndex] * 100f
                           + Mathf.Abs(doorX - transform.position.x) / maxSpeed
                           + (IsReachable(doorX) ? 0f : 50f);
                if (cost < bestCost)
                {
                    bestCost = cost;
                    targetDoor = door;
                }
            }
        }

        targetBelt = targetDoor.GetComponentInParent<ConveyorBelt>();
        aimOffset = RollAimOffset(targetDoor);
    }

    float RollAimOffset(PortalDoor door)
    {
        if (Random.value >= missChance)
            return Random.Range(-aimJitter, aimJitter);

        // Line up just outside the overlap window so the jump whiffs past the door
        Collider doorCol = door.GetComponent<Collider>();
        float doorHalf = doorCol != null ? doorCol.bounds.extents.x : 0.5f;
        float myRadius = capsule != null ? capsule.radius * Mathf.Abs(transform.lossyScale.x) : 0.4f;
        float window = doorHalf + myRadius;
        float side = Random.value < 0.5f ? -1f : 1f;
        return side * Random.Range(window + 0.15f, window + 0.5f);
    }

    /// <summary>
    /// Teleports needed to reach the win floor from each floor. Taking a door on belt b
    /// moves you from floor b to floor (paired door's belt index).
    /// </summary>
    int[] ComputeHopsToWin()
    {
        int floorCount = generator.floors.Length;
        var hops = new int[floorCount];
        for (int i = 0; i < floorCount; i++) hops[i] = Unreachable;

        int win = WinFloor();
        if (win >= 0 && win < floorCount) hops[win] = 0;

        // Bellman-Ford style relaxation; the graph has only a handful of floors
        for (int pass = 0; pass < floorCount; pass++)
        {
            for (int b = 0; b < generator.BeltCount && b < floorCount; b++)
            {
                foreach (var door in generator.GetDoorsOnBelt(b))
                {
                    if (door == null || door.pairedDoor == null) continue;
                    int dest = door.pairedDoor.beltIndex;
                    if (dest < 0 || dest >= floorCount) continue;
                    if (hops[dest] + 1 < hops[b]) hops[b] = hops[dest] + 1;
                }
            }
        }
        return hops;
    }

    // ── Layout helpers ──────────────────────────────────────────────

    int CurrentFloor()
    {
        float feetY = transform.position.y;
        int floor = -1;
        for (int i = 0; i < generator.floors.Length; i++)
        {
            if (generator.floors[i] != null && FloorTopY(i) <= feetY + 0.2f)
                floor = i;
        }
        return floor;
    }

    int WinFloor()
    {
        WinTrigger win = FindObjectOfType<WinTrigger>();
        if (win == null) return generator.BeltCount - 1;

        int floor = -1;
        for (int i = 0; i < generator.floors.Length; i++)
        {
            if (generator.floors[i] != null && FloorTopY(i) <= win.transform.position.y)
                floor = i;
        }
        return floor;
    }

    float FloorTopY(int i)
    {
        Transform f = generator.floors[i];
        return f.position.y + f.lossyScale.y * 0.5f;
    }

    static float BeltVelocity(ConveyorBelt belt)
    {
        if (belt == null) return 0f;
        return belt.moveRight ? belt.speed : -belt.speed;
    }

    bool IsReachable(float x) => Mathf.Abs(x) <= reachableHalfWidth;

    /// <summary>
    /// True when no human is still in the race (e.g. removed after a tracking-loss timeout,
    /// while the scene waits to return to the lobby), or, if enabled, while any human is
    /// frozen for tracking loss.
    /// </summary>
    bool ShouldWaitForHumans()
    {
        bool anyHumanActive = false;
        foreach (var p in opponents)
        {
            if (p == null || p.isCpu || !p.isActiveAndEnabled) continue;
            anyHumanActive = true;
            if (pauseWhileOpponentTrackingLost && p.IsTrackingLost)
                return true;
        }
        return !anyHumanActive;
    }

    static float RandomIn(Vector2 range) => Random.Range(range.x, range.y);

    // ── Difficulty presets ──────────────────────────────────────────
    // Benchmarked against human runs of ~15–30s (CPU-only matches, median win time):
    // Easy ≈ 35s, Normal ≈ 19s, Hard ≈ 12s. Spread mostly comes from the door layout.

    public void ApplyPreset(Difficulty difficulty)
    {
        switch (difficulty)
        {
            case Difficulty.Easy:
                speedMultiplier = 0.4f;
                reactionTimeRange = new Vector2(1.8f, 2.8f);
                jumpHesitationRange = new Vector2(0.5f, 1.0f);
                wrongDoorChance = 0.4f;
                missChance = 0.4f;
                break;
            case Difficulty.Normal:
                speedMultiplier = 0.5f;
                reactionTimeRange = new Vector2(1.2f, 2.0f);
                jumpHesitationRange = new Vector2(0.3f, 0.7f);
                wrongDoorChance = 0.3f;
                missChance = 0.3f;
                break;
            case Difficulty.Hard:
                speedMultiplier = 0.65f;
                reactionTimeRange = new Vector2(0.7f, 1.2f);
                jumpHesitationRange = new Vector2(0.1f, 0.4f);
                wrongDoorChance = 0.15f;
                missChance = 0.15f;
                break;
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Preset: Easy")] void PresetEasy() => ApplyPresetWithUndo(Difficulty.Easy);
    [ContextMenu("Preset: Normal")] void PresetNormal() => ApplyPresetWithUndo(Difficulty.Normal);
    [ContextMenu("Preset: Hard")] void PresetHard() => ApplyPresetWithUndo(Difficulty.Hard);

    void ApplyPresetWithUndo(Difficulty difficulty)
    {
        UnityEditor.Undo.RecordObject(this, $"CPU Preset {difficulty}");
        ApplyPreset(difficulty);
        UnityEditor.EditorUtility.SetDirty(this);
    }

    void OnDrawGizmosSelected()
    {
        if (targetDoor == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position + Vector3.up, targetDoor.transform.position);
        Vector3 aim = new Vector3(targetDoor.transform.position.x + aimOffset, transform.position.y, transform.position.z);
        Gizmos.DrawWireSphere(aim, 0.2f);
    }
#endif
}
