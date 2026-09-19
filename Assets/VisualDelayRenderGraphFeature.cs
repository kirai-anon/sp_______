using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

[System.Serializable, VolumeComponentMenu("Custom Post-processing/Visual Delay (Unity 6)")]
public class VisualDelayVolume : VolumeComponent, IPostProcessComponent
{
    public ClampedFloatParameter delay = new ClampedFloatParameter(0.85f, 0f, 0.98f);
    public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f);

    public bool IsActive() => intensity.value > 0f;
    public bool IsTileCompatible() => false;
}

public class VisualDelayRenderGraphFeature : ScriptableRendererFeature
{
    private class VisualDelayPass : ScriptableRenderPass
    {
        private Material m_Material;
        private RTHandle m_HistoryRTHandle;
        private bool m_IsHistoryInitialized = false;

        private static readonly int DelayProperty = Shader.PropertyToID("_Delay");
        private static readonly int IntensityProperty = Shader.PropertyToID("_Intensity");
        private static readonly int HistoryTexProperty = Shader.PropertyToID("_HistoryTex");

        public VisualDelayPass(Material material)
        {
            m_Material = material;
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        }

        private class PassData
        {
            public TextureHandle source;
            public TextureHandle historyInput;
            public TextureHandle historyOutput;
            public Material material;
            public float delay;
            public float intensity;
            public bool isHistoryInitialized;
            public RTHandle rawHistoryHandle;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (m_Material == null) return;

            var stack = VolumeManager.instance.stack;
            var volume = stack.GetComponent<VisualDelayVolume>();
            if (volume == null || !volume.IsActive()) return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            TextureHandle activeSource = resourceData.activeColorTexture;
            RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;

            // Allocate persistent history RTHandle if needed or resized
            bool didReallocate = RenderingUtils.ReAllocateIfNeeded(ref m_HistoryRTHandle, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_2DDelayHistory");
            if (didReallocate)
            {
                m_IsHistoryInitialized = false;
            }

            // Create temp target for the current frame's processed output
            TextureHandle tempTarget = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_2DDelayTemp", true);
            TextureHandle historyHandle = renderGraph.ImportTexture(m_HistoryRTHandle);

            // If history isn't initialized yet, seed it using a copy pass from the source on frame 1
            if (!m_IsHistoryInitialized)
            {
                using (var builder = renderGraph.AddRasterRenderPass<PassData>("2D Visual Delay Init", out var passData))
                {
                    passData.source = activeSource;
                    passData.historyOutput = historyHandle;

                    builder.UseTexture(passData.source, AccessFlags.Read);
                    builder.SetRenderAttachment(passData.historyOutput, 0, AccessFlags.Write);

                    builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                    {
                        Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), 0, false);
                    });
                }
                m_IsHistoryInitialized = true;
            }

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("2D Visual Delay Pass", out var passData))
            {
                passData.source = activeSource;
                passData.historyInput = historyHandle;
                passData.historyOutput = historyHandle;
                passData.material = m_Material;
                passData.delay = volume.delay.value;
                passData.intensity = volume.intensity.value;
                passData.isHistoryInitialized = m_IsHistoryInitialized;
                passData.rawHistoryHandle = m_HistoryRTHandle;

                builder.UseTexture(passData.source, AccessFlags.Read);
                builder.UseTexture(passData.historyInput, AccessFlags.Read);

                // Set tempTarget as the render target for this raster pass
                builder.SetRenderAttachment(tempTarget, 0, AccessFlags.Write);
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                {
                    data.material.SetFloat(DelayProperty, data.delay);
                    data.material.SetFloat(IntensityProperty, data.intensity);

                    // Bind history texture globally for your custom shader
                    context.cmd.SetGlobalTexture(HistoryTexProperty, data.historyInput);
                    data.material.SetTexture("_BlitTexture", data.source);

                    // 1. Draw/Blit current scene combined with history into tempTarget (active render attachment)
                    Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, 0);
                });
            }

            // Update history buffer for the *next* frame by copying the newly rendered output (tempTarget) back into history RTHandle
            // Render Graph handles this safely via a standard copy/blit pass or post-pass callback.
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("2D Visual Delay History Update", out var passData))
            {
                passData.source = tempTarget;
                passData.historyOutput = historyHandle;
                passData.rawHistoryHandle = m_HistoryRTHandle;

                builder.UseTexture(passData.source, AccessFlags.Read);
                builder.SetRenderAttachment(passData.historyOutput, 0, AccessFlags.Write);

                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                {
                    // Copy current frame result back to history texture for the next frame
                    Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), 0, false);
                });
            }

            // Redirect camera color to our processed temp target
            resourceData.cameraColor = tempTarget;
        }

        public void Cleanup()
        {
            m_HistoryRTHandle?.Release();
            m_HistoryRTHandle = null;
            m_IsHistoryInitialized = false;
        }
    }

    [SerializeField] private Shader m_Shader;
    private Material m_Material;
    private VisualDelayPass m_RenderPass;

    public override void Create()
    {
        if (m_Shader == null) m_Shader = Shader.Find("Hidden/PostProcess/VisualDelayUnity6");
        if (m_Shader != null) m_Material = CoreUtils.CreateEngineMaterial(m_Shader);

        m_RenderPass = new VisualDelayPass(m_Material);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.cameraType == CameraType.Game)
        {
            renderer.EnqueuePass(m_RenderPass);
        }
    }

    protected override void Dispose(bool disposing)
    {
        m_RenderPass?.Cleanup();
        CoreUtils.Destroy(m_Material);
    }
}