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
    bool frozen;
    public bool IsFrozen => frozen;
    [HideInInspector] public bool useBodyTracking = false;
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
        if (frozen) return;
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.Playing)
            return;

        float moveX = 0f;

        if (!useBodyTracking)
        {
            if (Input.GetKey(leftKey)) moveX -= 1f;
            if (Input.GetKey(rightKey)) moveX += 1f;

            rb.velocity = new Vector3(moveX * moveSpeed, rb.velocity.y, 0f);

            if (Input.GetKeyDown(jumpKey) && isGrounded)
            {
                rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            }
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

    void CheckGround()
    {
        // Don't update ground state while frozen (teleport animation) —
        // otherwise the raycast can detect the floor at the destination,
        // creating a false ground→air transition that resets teleportedThisJump.
        if (frozen) return;

        float scaleY = transform.lossyScale.y;
        float halfHeight = capsule != null ? capsule.height * 0.5f * scaleY : 0.5f;
        float centerY = capsule != null ? capsule.center.y * scaleY : 0f;
        Vector3 origin = transform.position + Vector3.up * (centerY - halfHeight + 0.05f);
        isGrounded = Physics.Raycast(origin, Vector3.down, groundCheckDistance, groundLayer);

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

        frozen = true;
        rb.velocity = Vector3.zero;
        rb.isKinematic = true;
        teleportedThisJump = true;

        // Character center offset (pivot is at feet, center is higher)
        float centerOffsetY = capsule != null ? capsule.center.y * originalScale.y : 0.5f;
        Vector3 shrinkOrigin = transform.position;

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

            yield return null;
        }
        transform.localScale = Vector3.zero;

        // --- Move to destination ---
        // Flag for BodyTrackingInput to recalculate offset from actual tracked
        // position on the next frame, instead of accumulating deltas which
        // drift in builds due to frame-rate-dependent trigger timing.
        needsOffsetRecalculation = true;
        transform.position = new Vector3(destination.x, destination.y, -0.3f);

        // Brief pause at zero scale
        yield return new WaitForSeconds(0.05f);
        if (gen != resetGeneration) yield break;

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

            yield return null;
        }
        transform.localScale = originalScale;
        Vector3 finalPos = new Vector3(growOrigin.x, growOrigin.y, -0.3f);
        transform.position = finalPos;

        // Sync physics position before switching off kinematic to prevent
        // interpolation from using a stale position (causes offset in builds).
        rb.position = finalPos;
        rb.velocity = Vector3.zero;
        rb.isKinematic = false;
        frozen = false;
    }

    public void FreezeForTrackingLoss()
    {
        frozen = true;
        rb.velocity = Vector3.zero;
        rb.isKinematic = true;
    }

    public void UnfreezeFromTrackingLoss()
    {
        frozen = false;
        rb.isKinematic = false;
    }

    public void FreezeForWin()
    {
        frozen = true;
        rb.velocity = Vector3.zero;
        rb.isKinematic = true;
        SetAllRenderersVisible(true);
    }

    public void ResetPlayer(Vector3 spawnPosition)
    {
        resetGeneration++;
        StopAllCoroutines();

        frozen = false;
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
