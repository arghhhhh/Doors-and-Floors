using UnityEngine;

public class PortalDoor : MonoBehaviour
{
    public PortalDoor pairedDoor;
    public Color doorColor = Color.white;
    public float cooldownTime = 0.5f;
    public float teleportYOffset = -0.5f; // Appear at the door, then fall naturally

    float lastTeleportTime = -1f;
    Renderer doorRenderer;

    void Awake()
    {
        doorRenderer = GetComponent<Renderer>();
    }

    void Start()
    {
        if (doorRenderer != null)
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", doorColor);
            doorRenderer.SetPropertyBlock(block);
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
        float delay = GameManager.Instance != null ? GameManager.Instance.teleportDelay : 0f;

        if (delay > 0f)
            player.StartDelayedTeleport(dest, delay);
        else
            player.TeleportTo(dest);
    }

    public void SetColor(Color color)
    {
        doorColor = color;
        if (doorRenderer != null)
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            doorRenderer.SetPropertyBlock(block);
        }
    }
}
