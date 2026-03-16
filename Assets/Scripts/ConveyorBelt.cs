using UnityEngine;

public class ConveyorBelt : MonoBehaviour
{
    public float speed = 1.5f;
    public float leftBound = -6f;
    public float rightBound = 6f;
    public bool moveRight = true;

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.Playing)
            return;

        float direction = moveRight ? 1f : -1f;
        float delta = speed * direction * Time.deltaTime;

        foreach (Transform child in transform)
        {
            Vector3 pos = child.localPosition;
            pos.x += delta;

            // Wrap around
            float width = rightBound - leftBound;
            if (pos.x > rightBound) pos.x -= width;
            else if (pos.x < leftBound) pos.x += width;

            child.localPosition = pos;
        }
    }
}
