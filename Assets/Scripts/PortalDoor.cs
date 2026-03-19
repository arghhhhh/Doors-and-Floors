using UnityEngine;

public class PortalDoor : MonoBehaviour
{
    public PortalDoor pairedDoor;
    public Color doorColor = Color.white;
    public float cooldownTime = 0.5f;
    public float teleportYOffset = -0.5f; // Appear at the door, then fall naturally

    // Per-door spiral parameters (set by DoorPairGenerator before Start)
    [HideInInspector] public float spiralSpeed = 1.0f;
    [HideInInspector] public float spiralArms = 6.0f;
    [HideInInspector] public float spiralRings = 5.0f;
    [HideInInspector] public float spiralBands = 10.0f;
    [HideInInspector] public float spiralAngle = 1.0472f;
    [HideInInspector] public float spiralBrightness = 1.3f;
    [HideInInspector] public Color edgeColor = new Color(0.12f, 0.1f, 0.08f);

    // Swivel animation (set by DoorPairGenerator)
    [HideInInspector] public float swivelCenter = 90f;   // midpoint angle
    [HideInInspector] public float swivelRange = 25f;    // ± degrees from center
    [HideInInspector] public float swivelSpeed = 1.0f;   // oscillation speed
    [HideInInspector] public float swivelOffset = 0f;    // phase offset

    // Shader reference to ensure it's included in builds (Shader.Find is stripped)
    [SerializeField] public Shader spiralShader;

    static Material sharedSpiralMaterial;

    float lastTeleportTime = -1f;
    Renderer doorRenderer;

    void Awake()
    {
        doorRenderer = GetComponent<Renderer>();
    }

    void Start()
    {
        ApplySpiralMaterial();
    }

    void Update()
    {
        // Swivel: oscillate Y rotation around swivelCenter
        float angle = swivelCenter + Mathf.Sin(Time.time * swivelSpeed + swivelOffset) * swivelRange;
        transform.localRotation = Quaternion.Euler(0f, angle, 0f);
    }

    void ApplySpiralMaterial()
    {
        if (doorRenderer == null) return;

        // Create shared material from the serialized shader reference
        if (sharedSpiralMaterial == null)
        {
            if (spiralShader != null)
                sharedSpiralMaterial = new Material(spiralShader);
        }

        if (sharedSpiralMaterial != null)
        {
            Material mat = new Material(sharedSpiralMaterial);
            mat.SetColor("_BaseColor", doorColor);
            mat.SetFloat("_Speed", spiralSpeed);
            mat.SetFloat("_NumArms", spiralArms);
            mat.SetFloat("_NumRings", spiralRings);
            mat.SetFloat("_NumBands", spiralBands);
            mat.SetFloat("_SpiralAngle", spiralAngle);
            mat.SetFloat("_Brightness", spiralBrightness);
            mat.SetColor("_EdgeColor", edgeColor);
            doorRenderer.material = mat;
        }
    }

    void OnTriggerStay(Collider other)
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.Playing)
            return;
        if (pairedDoor == null) return;

        if (Time.time - lastTeleportTime < cooldownTime) return;
        if (Time.time - pairedDoor.lastTeleportTime < cooldownTime) return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        if (player.IsGrounded) return;
        if (!player.CanTeleport) return;

        lastTeleportTime = Time.time;
        pairedDoor.lastTeleportTime = Time.time;

        Vector3 dest = pairedDoor.transform.position + Vector3.up * teleportYOffset;

        SFXManager.Instance?.Play(SFXManager.Instance?.sfxTeleport);
        player.TeleportTo(transform, dest);
    }

    public void SetColor(Color color)
    {
        doorColor = color;
        if (doorRenderer != null && doorRenderer.material != null
            && doorRenderer.material.HasProperty("_BaseColor"))
        {
            doorRenderer.material.SetColor("_BaseColor", color);
        }
    }
}
 
