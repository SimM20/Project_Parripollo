using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Renderer feature del Renderer2D: mientras el fondo de la shop está visible
/// (<see cref="ShopBackdropCapture.IsActive"/>) copia el color de la cámara a la textura global
/// <c>_ShopBackdropTex</c>. El shader SH_ShopDither la usa como "lo que hay detrás" del fondo.
///
/// Corre con todos los sprites del mundo ya dibujados y solo en cámaras de juego: la cámara de la
/// Scene view dibuja el canvas como geometría y capturaría la UI. Fuera de la shop no encola nada,
/// así que el resto del juego no paga la copia.
/// </summary>
public class ShopBackdropCaptureFeature : ScriptableRendererFeature
{
    [Tooltip("Momento del frame en el que se copia la cámara. Después de los transparentes ya está todo el mundo dibujado.")]
    [SerializeField] private RenderPassEvent captureEvent = RenderPassEvent.AfterRenderingTransparents;

    private CapturePass capturePass;

    public override void Create()
    {
        capturePass = new CapturePass { renderPassEvent = captureEvent };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!ShouldCapture(in renderingData.cameraData))
            return;

        capturePass.ConfigureInput(ScriptableRenderPassInput.Color);
        renderer.EnqueuePass(capturePass);
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        if (ShouldCapture(in renderingData.cameraData))
            capturePass.SetSource(renderer.cameraColorTargetHandle);
    }

    protected override void Dispose(bool disposing)
    {
        capturePass?.Dispose();
    }

    private static bool ShouldCapture(in CameraData cameraData)
    {
        return ShopBackdropCapture.IsActive && cameraData.cameraType == CameraType.Game;
    }

    private class CapturePass : ScriptableRenderPass
    {
        private static readonly int BackdropTexId = Shader.PropertyToID("_ShopBackdropTex");

        private RTHandle source;
        private RTHandle destination;

        public CapturePass()
        {
            profilingSampler = new ProfilingSampler("Shop Backdrop Capture");
        }

        public void SetSource(RTHandle cameraColor)
        {
            source = cameraColor;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            RenderingUtils.ReAllocateIfNeeded(ref destination, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_ShopBackdropTex");

            ConfigureTarget(destination);
            ConfigureClear(ClearFlag.None, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (source == null)
                return;

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, profilingSampler))
            {
                Blitter.BlitCameraTexture(cmd, source, destination);
                cmd.SetGlobalTexture(BackdropTexId, destination.nameID);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            destination?.Release();
            destination = null;
        }
    }
}
