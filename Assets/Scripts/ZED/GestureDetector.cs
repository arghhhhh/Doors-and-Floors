using UnityEngine;
using sl;

/// <summary>
/// Static utility for detecting gestures from ZED body keypoints.
/// No MonoBehaviour — call methods directly.
/// </summary>
public static class GestureDetector
{
    // Indices used repeatedly
    const int neckIdx = (int)BODY_38_PARTS.NECK;
    const int leftShoulderIdx = (int)BODY_38_PARTS.LEFT_SHOULDER;
    const int rightShoulderIdx = (int)BODY_38_PARTS.RIGHT_SHOULDER;
    const int leftElbowIdx = (int)BODY_38_PARTS.LEFT_ELBOW;
    const int rightElbowIdx = (int)BODY_38_PARTS.RIGHT_ELBOW;
    const int leftWristIdx = (int)BODY_38_PARTS.LEFT_WRIST;
    const int rightWristIdx = (int)BODY_38_PARTS.RIGHT_WRIST;

    /// <summary>
    /// Detects the "field goal" gesture: both arms raised above the neck.
    /// Uses the neck keypoint (skeleton-based, not face-detection) as the reference
    /// so it works reliably even in poor lighting where face keypoints drop out.
    /// When wrists are low-confidence (e.g. hands out of frame), falls back to
    /// elbow positions or extrapolates from shoulder→elbow direction.
    /// </summary>
    public static bool IsFieldGoalGesture(
        Vector3[] keypoints,
        float[] confidences,
        float minConfidence = 50f,
        float minHeightAboveNeck = 0.15f)
    {
        if (keypoints == null || confidences == null)
            return false;

        if (keypoints.Length <= rightWristIdx || confidences.Length <= rightWristIdx)
            return false;

        // Neck is the reference point — comes from body skeleton, not face detection
        if (confidences[neckIdx] < minConfidence) return false;
        if (!float.IsFinite(keypoints[neckIdx].y)) return false;
        float neckY = keypoints[neckIdx].y;

        // Fallback keypoints (elbow, shoulder) use a lower threshold —
        // when hands are out of frame the whole arm skeleton loses some confidence
        float fallbackConf = minConfidence * 0.5f;

        // Get the best estimate of each hand's Y position
        float leftY = EstimateHandY(keypoints, confidences,
            leftShoulderIdx, leftElbowIdx, leftWristIdx, minConfidence, fallbackConf);
        float rightY = EstimateHandY(keypoints, confidences,
            rightShoulderIdx, rightElbowIdx, rightWristIdx, minConfidence, fallbackConf);

        if (!float.IsFinite(leftY) || !float.IsFinite(rightY))
            return false;

        return (leftY - neckY >= minHeightAboveNeck)
            && (rightY - neckY >= minHeightAboveNeck);
    }

    /// <summary>
    /// Returns the best Y estimate for one hand, using a cascade:
    /// 1. Wrist (if confident) — exact position
    /// 2. Extrapolate from shoulder→elbow (if both confident) — extends the arm direction
    /// 3. Elbow alone (if confident) — conservative fallback
    /// Returns NaN if nothing is usable.
    /// </summary>
    static float EstimateHandY(
        Vector3[] kp, float[] conf,
        int shoulderIdx, int elbowIdx, int wristIdx,
        float wristMinConf, float fallbackMinConf)
    {
        bool wristOk = conf[wristIdx] >= wristMinConf && float.IsFinite(kp[wristIdx].y);
        bool elbowOk = conf[elbowIdx] >= fallbackMinConf && float.IsFinite(kp[elbowIdx].y);
        bool shoulderOk = conf[shoulderIdx] >= fallbackMinConf && float.IsFinite(kp[shoulderIdx].y);

        // Best case: wrist is visible
        if (wristOk)
            return kp[wristIdx].y;

        // Extrapolate: shoulder→elbow direction, extend by forearm length (~same as upper arm)
        if (shoulderOk && elbowOk)
        {
            Vector3 dir = kp[elbowIdx] - kp[shoulderIdx];
            return kp[elbowIdx].y + dir.y; // project one more segment
        }

        // Last resort: elbow alone (hands are likely above elbow if arms are raised)
        if (elbowOk)
            return kp[elbowIdx].y;

        return float.NaN;
    }

    /// <summary>
    /// Returns a diagnostic string showing all relevant keypoint confidences,
    /// positions, estimated hand Y, and the final gesture result.
    /// Call from OnGUI or Debug.Log for troubleshooting.
    /// </summary>
    public static string GetDiagnostics(
        Vector3[] keypoints,
        float[] confidences,
        float minConfidence = 50f,
        float minHeightAboveNeck = 0.15f)
    {
        if (keypoints == null || confidences == null)
            return "null keypoints/confidences";
        if (keypoints.Length <= rightWristIdx)
            return $"keypoints.Length={keypoints.Length}, need>{rightWristIdx}";

        float fallbackConf = minConfidence * 0.5f;
        float neckY = keypoints[neckIdx].y;

        float leftY = EstimateHandY(keypoints, confidences,
            leftShoulderIdx, leftElbowIdx, leftWristIdx, minConfidence, fallbackConf);
        float rightY = EstimateHandY(keypoints, confidences,
            rightShoulderIdx, rightElbowIdx, rightWristIdx, minConfidence, fallbackConf);

        string LeftSource() {
            if (confidences[leftWristIdx] >= minConfidence) return "wrist";
            if (confidences[leftShoulderIdx] >= fallbackConf && confidences[leftElbowIdx] >= fallbackConf) return "extrap";
            if (confidences[leftElbowIdx] >= fallbackConf) return "elbow";
            return "NONE";
        }
        string RightSource() {
            if (confidences[rightWristIdx] >= minConfidence) return "wrist";
            if (confidences[rightShoulderIdx] >= fallbackConf && confidences[rightElbowIdx] >= fallbackConf) return "extrap";
            if (confidences[rightElbowIdx] >= fallbackConf) return "elbow";
            return "NONE";
        }

        bool result = IsFieldGoalGesture(keypoints, confidences, minConfidence, minHeightAboveNeck);

        return $"GESTURE {(result ? "YES" : "no ")} | " +
               $"neck conf={confidences[neckIdx]:F0} y={neckY:F2} | " +
               $"L: shldr={confidences[leftShoulderIdx]:F0} elbow={confidences[leftElbowIdx]:F0} wrist={confidences[leftWristIdx]:F0} " +
               $"estY={leftY:F2} src={LeftSource()} delta={leftY - neckY:F2} | " +
               $"R: shldr={confidences[rightShoulderIdx]:F0} elbow={confidences[rightElbowIdx]:F0} wrist={confidences[rightWristIdx]:F0} " +
               $"estY={rightY:F2} src={RightSource()} delta={rightY - neckY:F2}";
    }
}
