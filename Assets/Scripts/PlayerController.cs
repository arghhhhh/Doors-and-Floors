using System;
using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 6f;
    public float jumpForce = 8f;

    [Header("Ground Detection")]
    public float groundCheckDistance = 0.15f;
    public LayerMask groundLayer;

    [Header("Controls")]
    public KeyCode leftKey = KeyCode.A;
    public KeyCode rightKey = KeyCode.D;
    public KeyCode jumpKey = KeyCode.W;

    [Header("Portal Animation")]
    public float portalShrinkDuration = 0.2f;
    public float portalGrowDuration = 0.2f;

    [Header("Identity")]
    public int playerNumber = 1;
    public Texture2D profilePhoto;

    [Header("Appearance")]
    [Tooltip("Hue shift applied to the player's materials (0 = no change, 0.6 = yellow→purple)")]
    [Range(0f, 1f)]
    public float hueShift = 0f;
    [Tooltip("HueShiftLit shader — must be assigned so it's included in builds")]
    public Shader hueShiftShader;

    [HideInInspector] public Rigidbody rb;
    bool isGrounded;
    bool wasGrounded;
    public bool IsGrounded => isGrounded;
    CapsuleCollider capsule;
    Renderer playerRenderer;
    Animator animator;
    Transform modelTransform;
    // Portal animation, tracking loss and win/tutorial each hold their own freeze so
    // releasing one (e.g. tracking recovered mid-teleport) can't cancel another
    [Flags] enum FreezeReason { None = 0, Portal = 1, TrackingLoss = 2, Win = 4 }
    FreezeReason freezeReasons;
    public bool IsFrozen => freezeReasons != FreezeReason.None;
    public bool IsTrackingLost => (freezeReasons & FreezeReason.TrackingLoss) != 0;

    /// <summary>Raised by ResetPlayer so input adapters can clear their own state.</summary>
    public event Action OnReset;
    /// <summary>
    /// True when movement comes from an input adapter (BodyTrackingInput, CpuPlayerInput)
    /// via SetHorizontalVelocity/TryJump instead of the keyboard.
    /// </summary>
    [HideInInspector] public bool useExternalInput = false;
    /// <summary>Set by GameScreenManager when this player is driven by CpuPlayerInput.</summary>
    [HideInInspector] public bool isCpu = false;
    bool teleportedThisJump;
    bool hasLandedSinceReset;
    public bool CanTeleport => !teleportedThisJump && hasLandedSinceReset;
    int resetGeneration;

    /// <summary>
    /// X offset from teleports, used by BodyTrackingInput to keep
    /// the character near the portal exit rather than snapping back to the
    /// tracked body mapped position. Recalculated from actual tracking data
    /// after each teleport to avoid accumulation errors.
    /// </summary>
    [HideInInspector] public float trackingXOffset;

    /// <summary>
    /// Set after teleportation so BodyTrackingInput recalculates the offset
    /// from the actual tracked position rather than accumulating deltas.
    /// </summary>
    [HideInInspector] public bool needsOffsetRecalculation;

    Vector3 originalScale;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        playerRenderer = GetComponent<Renderer>();
        animator = GetComponentInChildren<Animator>();
        if (animator != null) modelTransform = animator.transform;

        // Zero-friction material so the player slides along walls instead of sticking
        if (capsule != null)
        {
            PhysicMaterial frictionless = new PhysicMaterial("PlayerNoFriction");
            frictionless.dynamicFriction = 0f;
            frictionless.staticFriction = 0f;
            frictionless.frictionCombine = PhysicMaterialCombine.Minimum;
            capsule.material = frictionless;
        }

        originalScale = transform.localScale;

        rb.constraints = RigidbodyConstraints.FreezePositionZ
                       | RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        Physics.IgnoreLayerCollision(gameObject.layer, gameObject.layer, true);

        if (hueShift > 0f)
            ApplyHueShift();
    }

    void ApplyHueShift()
    {
        if (hueShiftShader == null)
        {
            Debug.LogWarning("[PlayerController] hueShiftShader not assigned.");
            return;
        }
        Shader hueShader = hueShiftShader;

        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            Material[] mats = r.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                Material clone = new Material(mats[i]);
                clone.shader = hueShader;
                clone.SetFloat("_HueShift", hueShift);
                mats[i] = clone;
            }
            r.materials = mats;
        }
    }

    /// <summary>
    /// Sets the profile photo (used for win screen and high scores, not displayed in-game).
    /// </summary>
    public void SetProfilePhoto(Texture2D photo)
    {
        profilePhoto = photo;
    }

    void FixedUpdate()
    {
        CheckGround();
    }

    void Update()
    {
        if (IsFrozen) return;
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.Playing)
            return;

        float moveX = 0f;

        if (!useExternalInput)
        {
            if (Input.GetKey(leftKey)) moveX -= 1f;
            if (Input.GetKey(rightKey)) moveX += 1f;

            SetHorizontalVelocity(moveX * moveSpeed);

            if (Input.GetKeyDown(jumpKey))
                TryJump();
        }
        else
        {
            moveX = rb.velocity.x / moveSpeed;
        }

        if (animator != null)
        {
            animator.SetFloat("Speed", Mathf.Abs(moveX));
            animator.SetBool("IsGrounded", isGrounded);
            animator.SetFloat("VelocityY", rb.velocity.y);
        }

        if (modelTransform != null)
        {
            float targetY;
            if (moveX > 0.1f) targetY = 90f;
            else if (moveX < -0.1f) targetY = 270f;
            else targetY = 180f;
            modelTransform.localRotation = Quaternion.Euler(0f, targetY, 0f);
        }

        Vector3 pos = transform.position;
        if (pos.z != -0.3f)
        {
            pos.z = -0.3f;
            transform.position = pos;
        }
    }

    /// <summary>Sets X velocity, keeping the current Y velocity. Ignored while frozen.</summary>
    public void SetHorizontalVelocity(float velocityX)
    {
        if (IsFrozen) return;
        rb.velocity = new Vector3(velocityX, rb.velocity.y, 0f);
    }

    /// <summary>Applies the jump impulse if grounded and not frozen. Returns true if it jumped.</summary>
    public bool TryJump()
    {
        if (IsFrozen || !isGrounded) return false;
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        GameVfx.Instance?.PlayJump(this);
        return true;
    }

    void CheckGround()
    {
        // Don't update ground state while frozen (teleport animation) —
        // otherwise the raycast can detect the floor at the destination,
        // creating a false ground→air transition that resets teleportedThisJump.
        if (IsFrozen) return;

        float scaleY = transform.lossyScale.y;
        float halfHeight = capsule != null ? capsule.height * 0.5f * scaleY : 0.5f;
        float centerY = capsule != null ? capsule.center.y * scaleY : 0f;
        float radius = capsule != null ? capsule.radius * transform.lossyScale.x : 0.25f;

        // Use the Rigidbody pose, not transform.position: with interpolation the transform
        // lags the physics state inside FixedUpdate (by several steps at high timeScale).
        // Start the ray well inside the capsule: if it starts inside the floor (fast landing,
        // slight penetration), Physics.Raycast ignores that collider and the player reads as
        // airborne for a step — which re-arms teleportedThisJump while still standing inside
        // the arrival door's trigger and sends them straight back through it.
        Vector3 capsuleBottom = rb.position + Vector3.up * (centerY - halfHeight);
        float lift = Mathf.Max(radius, 0.05f);
        // Grounded = floor within (groundCheckDistance - 0.05) below the capsule bottom (unchanged)
        isGrounded = Physics.Raycast(capsuleBottom + Vector3.up * lift, Vector3.down,
            lift + groundCheckDistance - 0.05f, groundLayer);

        if (isGrounded)
        {
            hasLandedSinceReset = true;
        }

        // Only reset teleportedThisJump on a fresh jump (ground → air transition)
        if (wasGrounded && !isGrounded)
        {
            teleportedThisJump = false;
        }

        wasGrounded = isGrounded;
    }

    public void TeleportTo(Transform entryDoor, Vector3 destination)
    {
        StartCoroutine(PortalAnimationCoroutine(entryDoor, destination));
    }

    // Legacy overload (keyboard mode / direct calls)
    public void TeleportTo(Vector3 destination)
    {
        StartCoroutine(PortalAnimationCoroutine(null, destination));
    }

    public void StartDelayedTeleport(Vector3 destination, float delay)
    {
        StartCoroutine(PortalAnimationCoroutine(null, destination));
    }

    IEnumerator PortalAnimationCoroutine(Transform entryDoor, Vector3 destination)
    {
        int gen = resetGeneration;

        AddFreeze(FreezeReason.Portal);
        teleportedThisJump = true;

        // Character center offset (pivot is at feet, center is higher)
        float centerOffsetY = capsule != null ? capsule.center.y * originalScale.y : 0.5f;
        Vector3 shrinkOrigin = transform.position;

        // Paired doors share a colour, so the entry door's colour also tints the exit burst
        PortalDoor entryPortal = entryDoor != null ? entryDoor.GetComponent<PortalDoor>() : null;
        Color portalColor = entryPortal != null ? entryPortal.doorColor : Color.white;
        GameVfx.Instance?.PlayPortalIn(this, entryDoor != null ? entryDoor.position : shrinkOrigin, portalColor);

        // --- Shrink at entry (pull toward door's live position) ---
        float elapsed = 0f;
        while (elapsed < portalShrinkDuration)
        {
            if (gen != resetGeneration) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / portalShrinkDuration);
            float eased = t * t;
            float s = 1f - t;

            transform.localScale = originalScale * s;

            // Track the door's current position each frame
            Vector3 doorTarget = entryDoor != null ? entryDoor.position : shrinkOrigin;
            doorTarget.z = -0.3f;

            Vector3 pos = Vector3.Lerp(shrinkOrigin, doorTarget, eased);
            pos.y += centerOffsetY * (1f - s);
            pos.z = -0.3f;
            transform.position = pos;
            rb.position = pos;

            yield return null;
        }
        transform.localScale = Vector3.zero;

        // --- Move to destination ---
        // Apply an approximate offset immediately so BodyTrackingInput doesn't
        // snap the player back before it can recalculate. The recalculation on
        // the next tracking frame will correct any imprecision.
        float deltaX = destination.x - transform.position.x;
        trackingXOffset += deltaX;
        needsOffsetRecalculation = true;
        Vector3 destPos = new Vector3(destination.x, destination.y, -0.3f);
        transform.position = destPos;
        // Sync rb.position so Rigidbody interpolation doesn't pull
        // transform.position back to the old location during the pause.
        rb.position = destPos;
        // Brief pause at zero scale
        yield return new WaitForSeconds(0.05f);
        if (gen != resetGeneration) yield break;

        GameVfx.Instance?.PlayPortalOut(this, destPos + Vector3.up * centerOffsetY, portalColor);

        // --- Grow at exit (scale around center) ---
        Vector3 growOrigin = transform.position;
        elapsed = 0f;
        while (elapsed < portalGrowDuration)
        {
            if (gen != resetGeneration) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / portalGrowDuration);
            float s = t;

            transform.localScale = originalScale * s;

            // Offset Y so character center stays at destination height
            Vector3 pos = growOrigin;
            pos.y += centerOffsetY * (1f - t); // lower feet as scale grows
            pos.z = -0.3f;
            transform.position = pos;
            rb.position = pos;

            yield return null;
        }
        transform.localScale = originalScale;
        Vector3 finalPos = new Vector3(growOrigin.x, growOrigin.y, -0.3f);
        transform.position = finalPos;

        // Sync physics position before switching off kinematic to prevent
        // interpolation from using a stale position (causes offset in builds).
        rb.position = finalPos;
        RemoveFreeze(FreezeReason.Portal);
    }

    void AddFreeze(FreezeReason reason)
    {
        freezeReasons |= reason;
        // Already kinematic when stacking a second reason; Unity warns on setting its velocity
        if (!rb.isKinematic)
        {
            rb.velocity = Vector3.zero;
            rb.isKinematic = true;
        }
    }

    void RemoveFreeze(FreezeReason reason)
    {
        freezeReasons &= ~reason;
        if (freezeReasons == FreezeReason.None)
        {
            rb.isKinematic = false;
            rb.velocity = Vector3.zero;
        }
    }

    public void FreezeForTrackingLoss() => AddFreeze(FreezeReason.TrackingLoss);

    public void UnfreezeFromTrackingLoss() => RemoveFreeze(FreezeReason.TrackingLoss);

    public void FreezeForWin()
    {
        AddFreeze(FreezeReason.Win);
        SetAllRenderersVisible(true);
    }

    public void ResetPlayer(Vector3 spawnPosition)
    {
        resetGeneration++;
        StopAllCoroutines();

        freezeReasons = FreezeReason.None;
        teleportedThisJump = false;
        hasLandedSinceReset = false;
        wasGrounded = false;
        trackingXOffset = 0f;
        needsOffsetRecalculation = false;
        transform.localScale = originalScale;
        rb.isKinematic = false;
        rb.velocity = Vector3.zero;
        transform.position = new Vector3(spawnPosition.x, spawnPosition.y, -0.3f);
        SetAllRenderersVisible(true);

        OnReset?.Invoke();
    }

    public void SetVisible(bool visible) => SetAllRenderersVisible(visible);

    void SetAllRenderersVisible(bool visible)
    {
        if (playerRenderer != null)
            playerRenderer.enabled = visible;

        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            r.enabled = visible;
        }
    }
}
