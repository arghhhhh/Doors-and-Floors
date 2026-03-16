using UnityEngine;
using sl;

/// <summary>
/// Static utility for detecting gestures from ZED body keypoints.
/// No MonoBehaviour — call methods directly.
/// </summary>
public static class GestureDetector
{
    /// <summary>
    /// Detects the "field goal" gesture: both wrists above nose by at least minHeightAboveNose meters.
    /// Uses BODY_38 format keypoint indices.
    /// </summary>
    /// <param name="keypoints">World-space keypoint positions (38 elements)</param>
    /// <param name="confidences">Per-keypoint confidence values (0-100)</param>
    /// <param name="minConfidence">Minimum confidence required on nose and both wrists</param>
    /// <param name="minHeightAboveNose">Minimum height (meters) wrists must be above nose</param>
    /// <returns>True if the field goal gesture is detected</returns>
    public static bool IsFieldGoalGesture(
        Vector3[] keypoints,
        float[] confidences,
        float minConfidence = 50f,
        float minHeightAboveNose = 0.15f)
    {
        if (keypoints == null || confidences == null)
            return false;

        int noseIdx = (int)BODY_38_PARTS.NOSE;           // 5
        int leftWristIdx = (int)BODY_38_PARTS.LEFT_WRIST;   // 16
        int rightWristIdx = (int)BODY_38_PARTS.RIGHT_WRIST;  // 17

        // Bounds check
        if (keypoints.Length <= rightWristIdx || confidences.Length <= rightWristIdx)
            return false;

        // Check confidence on all 3 keypoints
        if (confidences[noseIdx] < minConfidence) return false;
        if (confidences[leftWristIdx] < minConfidence) return false;
        if (confidences[rightWristIdx] < minConfidence) return false;

        // Check positions are valid
        if (!float.IsFinite(keypoints[noseIdx].y)) return false;
        if (!float.IsFinite(keypoints[leftWristIdx].y)) return false;
        if (!float.IsFinite(keypoints[rightWristIdx].y)) return false;

        float noseY = keypoints[noseIdx].y;

        // Both wrists must be above nose by at least minHeightAboveNose
        bool leftAbove = keypoints[leftWristIdx].y - noseY >= minHeightAboveNose;
        bool rightAbove = keypoints[rightWristIdx].y - noseY >= minHeightAboveNose;

        return leftAbove && rightAbove;
    }
}
