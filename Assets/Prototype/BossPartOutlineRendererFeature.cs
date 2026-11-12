using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class BossPartOutlineRendererFeature : ScriptableRendererFeature
{
    private sealed class MaskPass : ScriptableRenderPass
    {
        private readonly Material material;
        private FilteringSettings filtering;
        private readonly List<ShaderTagId> shaderTags=new List<ShaderTagId>
        {
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("UniversalForwardOnly"),
            new ShaderTagId("Universal2D"),
            new ShaderTagId("LightweightForward"),
            new ShaderTagId("SRPDefaultUnlit")
        };
        private RTHandle mask;
        private readonly ProfilingSampler sampler=new ProfilingSampler("Boss Part Outline Mask");

        public RTHandle Mask => mask;

        public MaskPass(Material maskMaterial,int layerMask)
        {
            material=maskMaterial;
            // Soul/ghost variants use transparent URP materials for most of their
            // body. Restricting this pass to the opaque queue outlines only their
            // opaque eyes and effect pieces, producing a broken silhouette.
            filtering=new FilteringSettings(RenderQueueRange.all,layerMask);
            renderPassEvent=RenderPassEvent.AfterRenderingOpaques;
        }

        public override void OnCameraSetup(CommandBuffer cmd,ref RenderingData renderingData)
        {
            RenderTextureDescriptor descriptor=renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits=0;
            descriptor.msaaSamples=1;
            descriptor.graphicsFormat=GraphicsFormat.R8G8B8A8_UNorm;
            RenderingUtils.ReAllocateIfNeeded(ref mask,descriptor,FilterMode.Point,TextureWrapMode.Clamp,name:"_BossPartOutlineMask");
            ConfigureTarget(mask);
            ConfigureClear(ClearFlag.Color,Color.clear);
        }

        public override void Execute(ScriptableRenderContext context,ref RenderingData renderingData)
        {
            if(material==null)return;
            DrawingSettings drawing=CreateDrawingSettings(
                shaderTags,ref renderingData,SortingCriteria.CommonTransparent);
            drawing.overrideMaterial=material;
            drawing.overrideMaterialPassIndex=0;
            CommandBuffer cmd=CommandBufferPool.Get();
            using(new ProfilingScope(cmd,sampler))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                context.DrawRenderers(renderingData.cullResults,ref drawing,ref filtering);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            mask?.Release();
            mask=null;
        }
    }

    private sealed class CompositePass : ScriptableRenderPass
    {
        private readonly Material material;
        private readonly MaskPass maskPass;
        private RTHandle source;
        private RTHandle temporary;
        private readonly ProfilingSampler sampler=new ProfilingSampler("Boss Part Outline Composite");

        public CompositePass(Material compositeMaterial,MaskPass sourceMask)
        {
            material=compositeMaterial;
            maskPass=sourceMask;
            renderPassEvent=RenderPassEvent.AfterRenderingTransparents;
        }

        public void Setup(RTHandle cameraColor)
        {
            source=cameraColor;
        }

        public override void OnCameraSetup(CommandBuffer cmd,ref RenderingData renderingData)
        {
            RenderTextureDescriptor descriptor=renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits=0;
            descriptor.msaaSamples=1;
            RenderingUtils.ReAllocateIfNeeded(ref temporary,descriptor,FilterMode.Bilinear,TextureWrapMode.Clamp,name:"_BossPartOutlineComposite");
        }

        public override void Execute(ScriptableRenderContext context,ref RenderingData renderingData)
        {
            if(material==null||source==null||maskPass.Mask==null)return;
            CommandBuffer cmd=CommandBufferPool.Get();
            using(new ProfilingScope(cmd,sampler))
            {
                cmd.SetGlobalTexture("_BossPartOutlineMask",maskPass.Mask);
                RenderTexture maskTexture=maskPass.Mask.rt;
                if(maskTexture!=null)
                    cmd.SetGlobalVector("_BossPartOutlineMask_TexelSize",new Vector4(
                        1f/maskTexture.width,1f/maskTexture.height,maskTexture.width,maskTexture.height));
                Blitter.BlitCameraTexture(cmd,source,temporary,material,0);
                Blitter.BlitCameraTexture(cmd,temporary,source);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            temporary?.Release();
            temporary=null;
        }
    }

    private Material maskMaterial;
    private Material compositeMaterial;
    private MaskPass maskPass;
    private CompositePass compositePass;

    public override void Create()
    {
        Shader maskShader=Shader.Find("Hidden/BombBrick/Boss Part Outline Mask");
        Shader compositeShader=Shader.Find("Hidden/BombBrick/Boss Part Outline Composite");
        if(maskShader!=null)maskMaterial=CoreUtils.CreateEngineMaterial(maskShader);
        if(compositeShader!=null)compositeMaterial=CoreUtils.CreateEngineMaterial(compositeShader);
        int layer=LayerMask.NameToLayer("ScreenSpaceOutline");
        maskPass=new MaskPass(maskMaterial,layer>=0?1<<layer:0);
        compositePass=new CompositePass(compositeMaterial,maskPass);
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer,in RenderingData renderingData)
    {
        if(renderingData.cameraData.cameraType==CameraType.Game)
            compositePass?.Setup(renderer.cameraColorTargetHandle);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer,ref RenderingData renderingData)
    {
        if(renderingData.cameraData.cameraType!=CameraType.Game||
           !ScreenSpaceOutlineTarget.HasActiveTargets||
           maskPass==null||compositePass==null)return;
        renderer.EnqueuePass(maskPass);
        renderer.EnqueuePass(compositePass);
    }

    protected override void Dispose(bool disposing)
    {
        maskPass?.Dispose();
        compositePass?.Dispose();
        CoreUtils.Destroy(maskMaterial);
        CoreUtils.Destroy(compositeMaterial);
    }
}
