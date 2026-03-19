using System.Collections.Generic;
using UnityEngine;

public class ConveyorBelt : MonoBehaviour
{
    public float speed = 1.5f;
    public float leftBound = -6f;
    public float rightBound = 6f;
    public bool moveRight = true;

    // Ghost clones for wrap-around effect
    Dictionary<Transform, GameObject> ghosts = new Dictionary<Transform, GameObject>();

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.Playing)
            return;

        float direction = moveRight ? 1f : -1f;
        float delta = speed * direction * Time.deltaTime;
        float width = rightBound - leftBound;

        // Collect children first to avoid modifying during iteration
        List<Transform> children = new List<Transform>();
        foreach (Transform child in transform)
            children.Add(child);

        foreach (Transform child in children)
        {
            // Skip ghost clones (they're managed separately)
            if (child.name.EndsWith("_ghost")) continue;

            Vector3 pos = child.localPosition;
            pos.x += delta;

            // Account for rotation: a rotated box's X extent depends on
            // both its X and Z scales projected onto the X axis.
            float rotY = child.localEulerAngles.y * Mathf.Deg2Rad;
            float doorHalfWidth = Mathf.Abs(child.localScale.x * 0.5f * Mathf.Cos(rotY))
                                + Mathf.Abs(child.localScale.z * 0.5f * Mathf.Sin(rotY));

            // Check if door is partially past either edge
            bool pastRight = pos.x + doorHalfWidth > rightBound;
            bool pastLeft = pos.x - doorHalfWidth < leftBound;

            if (pastRight || pastLeft)
            {
                // Ensure ghost exists
                if (!ghosts.TryGetValue(child, out GameObject ghost) || ghost == null)
                {
                    ghost = CreateGhost(child);
                    ghosts[child] = ghost;
                }

                // Position ghost on the opposite side
                Vector3 ghostPos = pos;
                ghostPos.x += pastRight ? -width : width;
                ghost.transform.localPosition = ghostPos;

                // Sync rotation with the real door
                ghost.transform.localRotation = child.localRotation;

                // If the door center has fully crossed, swap
                if (pos.x > rightBound + doorHalfWidth || pos.x < leftBound - doorHalfWidth)
                {
                    pos.x += pastRight ? -width : width;
                    DestroyGhost(child);
                }
            }
            else
            {
                // Fully inside bounds — remove ghost if it exists
                DestroyGhost(child);
            }

            child.localPosition = pos;
        }
    }

    GameObject CreateGhost(Transform original)
    {
        GameObject ghost = new GameObject(original.name + "_ghost");
        ghost.transform.SetParent(transform);
        ghost.transform.localScale = original.localScale;
        ghost.transform.localRotation = original.localRotation;

        // Copy the visual mesh
        MeshFilter srcFilter = original.GetComponent<MeshFilter>();
        Renderer srcRenderer = original.GetComponent<Renderer>();
        if (srcFilter != null && srcRenderer != null)
        {
            MeshFilter ghostFilter = ghost.AddComponent<MeshFilter>();
            ghostFilter.sharedMesh = srcFilter.sharedMesh;

            MeshRenderer ghostRenderer = ghost.AddComponent<MeshRenderer>();
            ghostRenderer.sharedMaterials = srcRenderer.sharedMaterials;
        }

        // No collider on ghosts — purely visual
        return ghost;
    }

    void DestroyGhost(Transform original)
    {
        if (ghosts.TryGetValue(original, out GameObject ghost))
        {
            if (ghost != null) Destroy(ghost);
            ghosts.Remove(original);
        }
    }

    void OnDestroy()
    {
        foreach (var kvp in ghosts)
        {
            if (kvp.Value != null) Destroy(kvp.Value);
        }
        ghosts.Clear();
    }
}
