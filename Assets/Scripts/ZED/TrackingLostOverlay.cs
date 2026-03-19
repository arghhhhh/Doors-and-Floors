using UnityEngine;

/// <summary>
/// Creates and manages a diagonal scan-line overlay quad on the player
/// when body tracking is lost. Attach to the player GameObject.
/// </summary>
public class TrackingLostOverlay : MonoBehaviour
{
    [Header("Overlay Size")]
    [Tooltip("Width of the overlay quad in world units")]
    public float overlayWidth = 1.4f;
    [Tooltip("Height of the overlay quad in world units")]
    public float overlayHeight = 2.2f;
    [Tooltip("Local Y offset (center of quad relative to player pivot)")]
    public float overlayYOffset = 1.0f;

    [Tooltip("Must be assigned so the shader is included in builds")]
    public Shader scanLinesShader;

    GameObject overlayQuad;
    Renderer overlayRenderer;
    Material overlayMaterial;
    bool isShowing;

    void Start()
    {
        CreateOverlay();
    }

    void CreateOverlay()
    {
        overlayQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        overlayQuad.name = "TrackingLostOverlay";
        overlayQuad.layer = LayerMask.NameToLayer("NoPixel");
        overlayQuad.transform.SetParent(transform);
        overlayQuad.transform.localPosition = new Vector3(0f, 1.5f, -0.15f);
        overlayQuad.transform.localScale = new Vector3(2f, 3f, 1f);

        // Remove collider
        Collider col = overlayQuad.GetComponent<Collider>();
        if (col != null) DestroyImmediate(col);

        overlayRenderer = overlayQuad.GetComponent<Renderer>();

        Shader shader = scanLinesShader;
        if (shader == null)
        {
            Debug.LogWarning("[TrackingLostOverlay] scanLinesShader not assigned, falling back to Unlit/Color");
            shader = Shader.Find("Unlit/Color");
        }

        overlayMaterial = new Material(shader);
        overlayRenderer.material = overlayMaterial;

        overlayQuad.SetActive(false);
        isShowing = false;
    }

    public void Show()
    {
        if (overlayQuad != null && !isShowing)
        {
            overlayQuad.SetActive(true);
            isShowing = true;
        }
    }

    public void Hide()
    {
        if (overlayQuad != null && isShowing)
        {
            overlayQuad.SetActive(false);
            isShowing = false;
        }
    }

    void OnDestroy()
    {
        if (overlayMaterial != null)
            Destroy(overlayMaterial);
    }
}
