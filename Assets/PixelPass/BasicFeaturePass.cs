using System.Collections.Generic;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEngine.Scripting.APIUpdating;

namespace UnityEngine.Experimental.Rendering.Universal
{
    public class BasicFeaturePass : ScriptableRenderPass
    {
        Material blitMat;
        float pixelDensity;

        ProfilingSampler m_ProfilingSampler;
        RenderStateBlock m_RenderStateBlock;
        List<ShaderTagId> m_ShaderTagIdList = new List<ShaderTagId>();
        FilteringSettings m_FilteringSettings;

        static int pixelTexID = Shader.PropertyToID("_PixelTexture");
        static int pixelDepthID = Shader.PropertyToID("_DepthTex");

        RTHandle cameraColorTarget;

        public void SetTarget(RTHandle colorTarget)
        {
            cameraColorTarget = colorTarget;
        }

        public BasicFeaturePass(RenderPassEvent renderEvent, Material bM, float pD, int lM) {
            m_ProfilingSampler = new ProfilingSampler("BasicFeature");
            this.renderPassEvent = renderEvent;
            blitMat = bM;
            pixelDensity = pD;
            if (blitMat != null)
                blitMat.SetFloat("_PixelDensity", pixelDensity);

            m_FilteringSettings = new FilteringSettings(RenderQueueRange.opaque, lM);

            m_ShaderTagIdList.Add(new ShaderTagId("UniversalForward"));
            m_ShaderTagIdList.Add(new ShaderTagId("LightweightForward"));
            m_ShaderTagIdList.Add(new ShaderTagId("SRPDefaultUnlit"));
            // VFX Graph outputs (and URP Unlit/Complex Lit) draw in UniversalForwardOnly;
            // without it they're skipped here, and the camera's own passes don't draw these layers
            m_ShaderTagIdList.Add(new ShaderTagId("UniversalForwardOnly"));

            m_RenderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            SortingCriteria sortingCriteria = SortingCriteria.CommonTransparent;

            DrawingSettings drawingSettings = CreateDrawingSettings(m_ShaderTagIdList, ref renderingData, sortingCriteria);
            ref CameraData cameraData = ref renderingData.cameraData;
            Camera camera = cameraData.camera;
            int pixelWidth = (int) (camera.pixelWidth / pixelDensity);
            int pixelHeight = (int) (camera.pixelHeight / pixelDensity);
            CommandBuffer cmd = CommandBufferPool.Get("BasicFeature");
            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                // Allocate temp RTs, set render target, and clear — then flush so the GPU
                // has the textures before DrawRenderers runs.
                cmd.GetTemporaryRT(pixelTexID, pixelWidth, pixelHeight, 0, FilterMode.Point);
                cmd.GetTemporaryRT(pixelDepthID, pixelWidth, pixelHeight, 24, FilterMode.Point, RenderTextureFormat.Depth);
                cmd.SetRenderTarget(pixelTexID, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                                    pixelDepthID, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                cmd.ClearRenderTarget(true, true, Color.clear);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                // Draw the layer-filtered objects into the low-res pixel RT.
                context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref m_FilteringSettings, ref m_RenderStateBlock);

                // Blit the pixel RT back onto the camera color target, then release temps.
                cmd.SetRenderTarget(cameraColorTarget, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
                cmd.Blit(new RenderTargetIdentifier(pixelTexID), BuiltinRenderTextureType.CurrentActive, blitMat);
                cmd.ReleaseTemporaryRT(pixelTexID);
                cmd.ReleaseTemporaryRT(pixelDepthID);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }
            CommandBufferPool.Release(cmd);
        }
    }
}
 
 
