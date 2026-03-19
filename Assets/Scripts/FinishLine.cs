using UnityEngine;

public class FinishLine : MonoBehaviour
{
    [Header("Slice Effect")]
    public float sliceFallSpeed = 3f;
    public float sliceRotateSpeed = 90f;

    bool sliced;
    GameObject leftHalf;
    GameObject rightHalf;
    Renderer ribbonRenderer;
    MeshFilter ribbonFilter;

    void Awake()
    {
        ribbonRenderer = GetComponentInChildren<Renderer>();
        ribbonFilter = GetComponentInChildren<MeshFilter>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (sliced) return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        float sliceX = other.transform.position.x;
        Slice(sliceX);
    }

    void Slice(float worldX)
    {
        sliced = true;

        // Get the mesh object (child with the ribbon mesh)
        Transform ribbonTransform = ribbonFilter != null ? ribbonFilter.transform : transform;

        // Create left half
        leftHalf = CreateHalf("FinishLine_Left", ribbonTransform, 1f, worldX);
        // Create right half
        rightHalf = CreateHalf("FinishLine_Right", ribbonTransform, 2f, worldX);

        // Hide the original ribbon
        if (ribbonRenderer != null)
            ribbonRenderer.enabled = false;
    }

    GameObject CreateHalf(string name, Transform source, float clipSide, float clipX)
    {
        GameObject half = new GameObject(name);
        half.transform.position = source.position;
        half.transform.rotation = source.rotation;
        half.transform.localScale = source.lossyScale;

        // Copy mesh
        MeshFilter mf = half.AddComponent<MeshFilter>();
        mf.sharedMesh = ribbonFilter.sharedMesh;

        // Copy material with clip parameters
        MeshRenderer mr = half.AddComponent<MeshRenderer>();
        Material mat = new Material(ribbonRenderer.material);
        mat.SetFloat("_ClipSide", clipSide);
        mat.SetFloat("_ClipX", clipX);
        mr.material = mat;

        // Add physics so it falls away
        Rigidbody rb = half.AddComponent<Rigidbody>();
        rb.useGravity = true;
        rb.mass = 0.2f;

        // Kick each half outward and spin it
        float dir = clipSide < 1.5f ? -1f : 1f;
        rb.velocity = new Vector3(dir * sliceFallSpeed * 0.5f, -sliceFallSpeed * 0.3f, 0f);
        rb.angularVelocity = new Vector3(
            Random.Range(-1f, 1f) * sliceRotateSpeed * Mathf.Deg2Rad,
            0f,
            dir * sliceRotateSpeed * Mathf.Deg2Rad
        );

        // No collision — just visual falling
        rb.isKinematic = false;
        Destroy(half.GetComponent<Collider>());

        // Clean up after a few seconds
        Destroy(half, 5f);

        return half;
    }

    public void ResetFinishLine()
    {
        sliced = false;

        if (leftHalf != null) Destroy(leftHalf);
        if (rightHalf != null) Destroy(rightHalf);

        if (ribbonRenderer != null)
            ribbonRenderer.enabled = true;
    }
}
