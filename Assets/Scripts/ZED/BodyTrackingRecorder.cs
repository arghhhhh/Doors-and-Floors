using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using sl;

/// <summary>
/// Records ZED body tracking keypoint data to a JSON file.
/// Press R to start a countdown, then records. Press R again to stop.
/// Saves to Assets/Recordings/ with a timestamped filename.
/// </summary>
public class BodyTrackingRecorder : MonoBehaviour
{
    [Header("Controls")]
    [Tooltip("Key to start/stop recording")]
    public KeyCode recordKey = KeyCode.R;

    [Header("Countdown")]
    [Tooltip("Seconds to wait before recording starts")]
    public float countdownDuration = 3f;

    [Header("Status")]
    [SerializeField] bool isRecording;
    [SerializeField] int frameCount;
    [SerializeField] float recordingDuration;

    ZEDTrackingProvider trackingProvider;
    RecordingData currentRecording;
    float recordingStartTime;

    // Countdown state
    bool isCountingDown;
    float countdownRemaining;

    [Serializable]
    public class RecordingFrame
    {
        public float time;
        public float[] keypointsFlat; // 38 * 3 floats (x,y,z)
        public float[] confidences;   // 38 floats
    }

    [Serializable]
    public class FloorPlaneInfo
    {
        public bool detected;
        public float playerHeight;
        // Gravity-alignment rotation (camera orientation relative to gravity)
        public float rotX, rotY, rotZ, rotW;
        // Floor plane normal in camera space
        public float normalX, normalY, normalZ;
        public float centerX, centerY, centerZ;
    }

    [Serializable]
    public class RecordingData
    {
        public string timestamp;
        public int keypointCount = 38;
        public FloorPlaneInfo floorPlane = new FloorPlaneInfo();
        public List<RecordingFrame> frames = new List<RecordingFrame>();
    }

    void Update()
    {
        if (trackingProvider == null)
        {
            trackingProvider = ZEDTrackingProvider.Instance;
            if (trackingProvider == null) return;
        }

        if (Input.GetKeyDown(recordKey))
        {
            if (isRecording)
                StopRecording();
            else if (isCountingDown)
                CancelCountdown();
            else
                StartCountdown();
        }

        if (isCountingDown)
        {
            countdownRemaining -= Time.deltaTime;
            if (countdownRemaining <= 0f)
            {
                isCountingDown = false;
                StartRecording();
            }
        }

        if (isRecording)
            CaptureFrame();
    }

    void StartCountdown()
    {
        isCountingDown = true;
        countdownRemaining = countdownDuration;
        Debug.Log($"[BodyTrackingRecorder] Recording in {countdownDuration}s... Press R to cancel.");
    }

    void CancelCountdown()
    {
        isCountingDown = false;
        Debug.Log("[BodyTrackingRecorder] Countdown cancelled.");
    }

    void StartRecording()
    {
        currentRecording = new RecordingData
        {
            timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")
        };

        // Detect floor plane for gravity alignment
        DetectFloorPlane();

        recordingStartTime = Time.time;
        isRecording = true;
        frameCount = 0;
        recordingDuration = 0f;
        Debug.Log("[BodyTrackingRecorder] Recording started. Press R to stop.");
    }

    void DetectFloorPlane()
    {
        if (trackingProvider == null || trackingProvider.zedManager == null) return;

        var zedCam = trackingProvider.zedManager.zedCamera;
        if (zedCam == null) return;

        var planeData = new ZEDPlaneGameObject.PlaneData();
        float playerHeight = 0f;

        sl.ERROR_CODE err = zedCam.findFloorPlane(ref planeData, out playerHeight,
            Quaternion.identity, Vector3.zero);

        if (err == sl.ERROR_CODE.SUCCESS)
        {
            currentRecording.floorPlane.detected = true;
            currentRecording.floorPlane.playerHeight = playerHeight;
            currentRecording.floorPlane.normalX = planeData.PlaneNormal.x;
            currentRecording.floorPlane.normalY = planeData.PlaneNormal.y;
            currentRecording.floorPlane.normalZ = planeData.PlaneNormal.z;
            currentRecording.floorPlane.centerX = planeData.PlaneCenter.x;
            currentRecording.floorPlane.centerY = planeData.PlaneCenter.y;
            currentRecording.floorPlane.centerZ = planeData.PlaneCenter.z;

            // Compute the rotation that aligns camera space with gravity
            // The floor normal in camera space tells us which direction is "up"
            Vector3 floorNormal = planeData.PlaneNormal;
            Quaternion gravityRot = Quaternion.FromToRotation(floorNormal, Vector3.up);
            currentRecording.floorPlane.rotX = gravityRot.x;
            currentRecording.floorPlane.rotY = gravityRot.y;
            currentRecording.floorPlane.rotZ = gravityRot.z;
            currentRecording.floorPlane.rotW = gravityRot.w;

            Debug.Log($"[BodyTrackingRecorder] Floor detected. Height={playerHeight:F2}m, Normal=({floorNormal.x:F2},{floorNormal.y:F2},{floorNormal.z:F2})");
        }
        else
        {
            currentRecording.floorPlane.detected = false;
            Debug.LogWarning($"[BodyTrackingRecorder] Floor plane detection failed: {err}. Using fallback orientation.");
        }
    }

    void StopRecording()
    {
        isRecording = false;
        Debug.Log($"[BodyTrackingRecorder] Recording stopped. {frameCount} frames, {recordingDuration:F1}s");

        if (frameCount > 0)
            SaveRecording();
        else
            Debug.LogWarning("[BodyTrackingRecorder] No frames captured — is a body visible?");
    }

    void CaptureFrame()
    {
        if (!trackingProvider.IsZEDReady) return;

        var bodies = trackingProvider.GetAllCurrentBodies();
        if (bodies.Count == 0) return;

        // Use first visible body
        DetectedBody body = null;
        foreach (var kvp in bodies)
        {
            body = kvp.Value;
            break;
        }

        Vector3[] keypoints = body.rawBodyData.keypoint;
        float[] confidences = body.rawBodyData.keypointConfidence;
        if (keypoints == null || keypoints.Length < 38) return;

        float time = Time.time - recordingStartTime;

        var frame = new RecordingFrame
        {
            time = time,
            keypointsFlat = new float[38 * 3],
            confidences = new float[38]
        };

        for (int i = 0; i < 38; i++)
        {
            frame.keypointsFlat[i * 3 + 0] = keypoints[i].x;
            frame.keypointsFlat[i * 3 + 1] = keypoints[i].y;
            frame.keypointsFlat[i * 3 + 2] = keypoints[i].z;
            frame.confidences[i] = confidences != null && i < confidences.Length ? confidences[i] : 0f;
        }

        currentRecording.frames.Add(frame);
        frameCount++;
        recordingDuration = time;
    }

    void SaveRecording()
    {
        string dir = Path.Combine(Application.dataPath, "Recordings");
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        string filename = $"mocap_{currentRecording.timestamp}.json";
        string path = Path.Combine(dir, filename);

        string json = JsonUtility.ToJson(currentRecording, true);
        File.WriteAllText(path, json);

        Debug.Log($"[BodyTrackingRecorder] Saved recording to Assets/Recordings/{filename}");

#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif
    }

    void OnGUI()
    {
        if (isCountingDown)
        {
            // Big countdown number in center of screen
            GUIStyle countdownStyle = new GUIStyle(GUI.skin.label);
            countdownStyle.fontSize = 120;
            countdownStyle.alignment = TextAnchor.MiddleCenter;
            countdownStyle.normal.textColor = Color.white;

            string countText = Mathf.CeilToInt(countdownRemaining).ToString();
            float w = 200f, h = 160f;
            UnityEngine.Rect r = new UnityEngine.Rect(
                (Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);

            // Drop shadow
            GUIStyle shadowStyle = new GUIStyle(countdownStyle);
            shadowStyle.normal.textColor = Color.black;
            GUI.Label(new UnityEngine.Rect(r.x + 3, r.y + 3, r.width, r.height), countText, shadowStyle);
            GUI.Label(r, countText, countdownStyle);
            return;
        }

        if (!isRecording) return;

        // Recording indicator
        float pulse = Mathf.PingPong(Time.time * 2f, 1f);
        GUI.color = new Color(1f, 0f, 0f, 0.5f + pulse * 0.5f);
        GUI.Box(new UnityEngine.Rect(10, 10, 200, 30), $"● REC  {recordingDuration:F1}s  ({frameCount} frames)");
        GUI.color = Color.white;
    }
}
