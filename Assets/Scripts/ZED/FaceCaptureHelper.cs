using UnityEngine;
using sl;

/// <summary>
/// Captures a cropped face texture from the ZED camera frame using body tracking 2D keypoints.
/// </summary>
public static class FaceCaptureHelper
{
    // Face-related keypoint indices in BODY_38 format
    static readonly int[] FaceKeypointIndices = new int[]
    {
        (int)BODY_38_PARTS.NOSE,       // 5
        (int)BODY_38_PARTS.LEFT_EYE,   // 6
        (int)BODY_38_PARTS.RIGHT_EYE,  // 7
        (int)BODY_38_PARTS.LEFT_EAR,   // 8
        (int)BODY_38_PARTS.RIGHT_EAR,  // 9
        (int)BODY_38_PARTS.NECK,       // 4
    };

    /// <summary>
    /// Captures the face region from the ZED camera's current frame for the given body.
    /// Derives the crop region from face-related 2D keypoints (nose, eyes, ears, neck).
    /// </summary>
    public static Texture2D CaptureFace(ZEDManager zedManager, DetectedBody body, int outputSize = 128)
    {
        if (zedManager == null || zedManager.zedCamera == null || body == null)
            return null;

        Vector2[] keypoints2D = body.rawBodyData.keypoint2D;
        if (keypoints2D == null || keypoints2D.Length < 38)
            return null;

        // Compute bounding box from face keypoints
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;
        int validCount = 0;

        for (int i = 0; i < FaceKeypointIndices.Length; i++)
        {
            int idx = FaceKeypointIndices[i];
            Vector2 p = keypoints2D[idx];

            // Skip invalid keypoints (negative coords = not detected)
            if (p.x < 0 || p.y < 0) continue;

            validCount++;
            if (p.x < minX) minX = p.x;
            if (p.y < minY) minY = p.y;
            if (p.x > maxX) maxX = p.x;
            if (p.y > maxY) maxY = p.y;
        }

        // Need at least nose + one other point
        if (validCount < 2)
            return null;

        // Get the GPU-side camera frame texture (LEFT view, auto-updated by ZED SDK)
        Texture2D frameTex = zedManager.zedCamera.CreateTextureImageType(VIEW.LEFT);
        if (frameTex == null || frameTex.width == 0)
            return null;

        int imgW = frameTex.width;
        int imgH = frameTex.height;

        // Expand the bounding box with generous padding to include full head
        float bboxW = maxX - minX;
        float bboxH = maxY - minY;
        float padX = Mathf.Max(bboxW * 0.5f, 30f);
        float padY = Mathf.Max(bboxH * 0.5f, 30f);

        // Extra padding above for forehead/hair
        float padTop = padY * 1.2f;

        int x0 = Mathf.Max(0, Mathf.FloorToInt(minX - padX));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(minY - padTop));
        int x1 = Mathf.Min(imgW, Mathf.CeilToInt(maxX + padX));
        int y1 = Mathf.Min(imgH, Mathf.CeilToInt(maxY + padY));

        int cropW = x1 - x0;
        int cropH = y1 - y0;
        if (cropW < 10 || cropH < 10)
            return null;

        // Make it square (centered)
        int side = Mathf.Max(cropW, cropH);
        int cx = x0 + cropW / 2;
        int cy = y0 + cropH / 2;
        x0 = Mathf.Max(0, cx - side / 2);
        y0 = Mathf.Max(0, cy - side / 2);
        x1 = Mathf.Min(imgW, x0 + side);
        y1 = Mathf.Min(imgH, y0 + side);
        cropW = x1 - x0;
        cropH = y1 - y0;

        Debug.Log($"[FaceCapture] Nose2D=({keypoints2D[(int)BODY_38_PARTS.NOSE].x:F0},{keypoints2D[(int)BODY_38_PARTS.NOSE].y:F0}) " +
                  $"imgSize={imgW}x{imgH} crop=({x0},{y0})-({x1},{y1})");

        // GPU texture -> readable CPU texture via RenderTexture blit
        // On D3D (Windows), Graphics.Blit flips the image vertically, so the
        // RenderTexture content already has Y=0 at bottom. Since keypoint2D uses
        // Y=0 at top (SDK image convention), we use y0 directly — the Blit flip
        // and the coordinate system difference cancel each other out.
        RenderTexture rt = RenderTexture.GetTemporary(imgW, imgH);
        Graphics.Blit(frameTex, rt);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D crop = new Texture2D(cropW, cropH, TextureFormat.RGBA32, false);
        crop.ReadPixels(new UnityEngine.Rect(x0, y0, cropW, cropH), 0, 0);
        crop.Apply();

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        // Flip vertically and resize to output size
        RenderTexture flipRT = RenderTexture.GetTemporary(outputSize, outputSize);
        // Blit with vertical flip: scale Y by -1, offset Y by 1
        Graphics.Blit(crop, flipRT, new Vector2(1, -1), new Vector2(0, 1));
        RenderTexture.active = flipRT;
        Texture2D result = new Texture2D(outputSize, outputSize, TextureFormat.RGBA32, false);
        result.ReadPixels(new UnityEngine.Rect(0, 0, outputSize, outputSize), 0, 0);
        result.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(flipRT);
        Object.Destroy(crop);
        crop = result;

        return crop;
    }
}
