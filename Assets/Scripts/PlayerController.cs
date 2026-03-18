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

    [Header("Identity")]
    public int playerNumber = 1;
    public Texture2D profilePhoto;

    [HideInInspector] public Rigidbody rb;
    bool isGrounded;
    public bool IsGrounded => isGrounded;
    CapsuleCollider capsule;
    Renderer playerRenderer;
    bool frozen;
    public bool IsFrozen => frozen;
    [HideInInspector] public bool useBodyTracking = false;
    bool teleportedThisJump;
    bool hasLandedSinceReset;
    public bool CanTeleport => !teleportedThisJump && hasLandedSinceReset;
    int resetGeneration; // increments on each reset to invalidate pending teleports

    /// <summary>
    /// Cumulative X offset from teleports, used by BodyTrackingInput to keep
    /// the character near the portal exit rather than snapping back to the
    /// tracked body's mapped position.
    /// </summary>
    [HideInInspector] public float trackingXOffset;
    GameObject profileQuad;
    Renderer profileRenderer;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        playerRenderer = GetComponent<Renderer>();

        rb.constraints = RigidbodyConstraints.FreezePositionZ
                       | RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Players pass through each other
        Physics.IgnoreLayerCollision(gameObject.layer, gameObject.layer, true);

        CreateProfileQuad();
    }

    void CreateProfileQuad()
    {
        if (profilePhoto == null) return;

        profileQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        profileQuad.name = $"ProfilePhoto_P{playerNumber}";
        profileQuad.transform.SetParent(transform);

        // Position on top of the capsule (capsule height is 2 unscaled, so top is at local y=1)
        // Quad sits just above the top
        float quadSize = 0.8f; // relative to player scale
        profileQuad.transform.localPosition = new Vector3(0f, 1.2f, -0.1f);
        profileQuad.transform.localScale = new Vector3(quadSize, quadSize, 1f);

        // Remove the collider immediately so it never conflicts with the Rigidbody
        Collider quadCol = profileQuad.GetComponent<Collider>();
        if (quadCol != null) DestroyImmediate(quadCol);

        // Apply the profile photo
        profileRenderer = profileQuad.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Unlit/Texture"));
        mat.mainTexture = profilePhoto;
        profileRenderer.material = mat;
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

        // Body tracking input is handled by BodyTrackingInput component
        if (!useBodyTracking)
        {
            float moveX = 0f;
            if (Input.GetKey(leftKey)) moveX -= 1f;
            if (Input.GetKey(rightKey)) moveX += 1f;

            rb.velocity = new Vector3(moveX * moveSpeed, rb.velocity.y, 0f);

            if (Input.GetKeyDown(jumpKey) && isGrounded)
            {
                rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            }
        }

        // Clamp Z position (slightly in front of doors so player renders on top)
        Vector3 pos = transform.position;
        if (pos.z != -0.3f)
        {
            pos.z = -0.3f;
            transform.position = pos;
        }
    }

    void CheckGround()
    {
        float halfHeight = capsule != null ? capsule.height * 0.5f * transform.lossyScale.y : 0.5f;
        Vector3 origin = transform.position + Vector3.down * (halfHeight - 0.05f);
        isGrounded = Physics.Raycast(origin, Vector3.down, groundCheckDistance, groundLayer);

        if (isGrounded)
        {
            teleportedThisJump = false;
            hasLandedSinceReset = true;
        }
    }

    public void TeleportTo(Vector3 destination)
    {
        float deltaX = destination.x - transform.position.x;
        trackingXOffset += deltaX;

        rb.isKinematic = true;
        rb.velocity = Vector3.zero;
        transform.position = new Vector3(destination.x, destination.y, -0.3f);
        teleportedThisJump = true;
        rb.isKinematic = false;
    }

    public void StartDelayedTeleport(Vector3 destination, float delay)
    {
        StartCoroutine(DelayedTeleportCoroutine(destination, delay));
    }

    IEnumerator DelayedTeleportCoroutine(Vector3 destination, float delay)
    {
        int gen = resetGeneration;

        frozen = true;
        rb.velocity = Vector3.zero;
        rb.isKinematic = true;
        SetAllRenderersVisible(false);

        yield return new WaitForSeconds(delay);

        // Abort if a reset happened during the wait
        if (gen != resetGeneration) yield break;

        float deltaX = destination.x - transform.position.x;
        trackingXOffset += deltaX;
        transform.position = new Vector3(destination.x, destination.y, -0.3f);
        teleportedThisJump = true;
        SetAllRenderersVisible(true);
        rb.isKinematic = false;
        frozen = false;
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
        // Invalidate any in-flight teleport coroutines
        resetGeneration++;
        StopAllCoroutines();

        frozen = false;
        teleportedThisJump = false;
        hasLandedSinceReset = false;
        trackingXOffset = 0f;
        rb.isKinematic = false;
        rb.velocity = Vector3.zero;
        transform.position = new Vector3(spawnPosition.x, spawnPosition.y, -0.3f);
        SetAllRenderersVisible(true);
    }

    void SetAllRenderersVisible(bool visible)
    {
        if (playerRenderer != null)
            playerRenderer.enabled = visible;
        if (profileRenderer != null)
            profileRenderer.enabled = visible;
    }
}
