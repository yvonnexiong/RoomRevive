// SPDX-License-Identifier: MIT

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GaussianSplatting.Runtime
{
    public class CameraArcSceneMaskRendererFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            [Tooltip("The shader used for the full-screen camera arc mask.")]
            public Shader maskShader;

            [Tooltip("When the mask is applied.")]
            public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;

            [Tooltip("Skip Scene View cameras.")]
            public bool skipSceneView = false;

            [Tooltip("Only run on cameras that have CameraArcSceneMask attached.")]
            public bool requireCameraArcSceneMaskComponent = true;
        }

        public Settings settings = new Settings();

        private CameraArcSceneMaskPass m_Pass;
        private Material m_Material;

        public override void Create()
        {
            if (settings.maskShader == null)
            {
                settings.maskShader = Shader.Find("Hidden/GaussianSplatting/CameraArcSceneMask");
            }

            if (settings.maskShader != null)
            {
                m_Material = CoreUtils.CreateEngineMaterial(settings.maskShader);
            }

            m_Pass = new CameraArcSceneMaskPass(m_Material)
            {
                renderPassEvent = settings.renderPassEvent
            };
        }

        public override void AddRenderPasses(
            ScriptableRenderer renderer,
            ref RenderingData renderingData)
        {
            if (m_Material == null)
            {
                return;
            }

            Camera camera = renderingData.cameraData.camera;

            if (camera == null)
            {
                return;
            }

            if (settings.skipSceneView && renderingData.cameraData.isSceneViewCamera)
            {
                return;
            }

            if (settings.requireCameraArcSceneMaskComponent)
            {
                CameraArcSceneMask mask = camera.GetComponent<CameraArcSceneMask>();

                if (mask == null || !mask.isActiveAndEnabled || !mask.m_EnableMask)
                {
                    return;
                }
            }

            renderer.EnqueuePass(m_Pass);
        }

        protected override void Dispose(bool disposing)
        {
            if (m_Pass != null)
            {
                m_Pass.Dispose();
            }

            CoreUtils.Destroy(m_Material);
        }

        private class CameraArcSceneMaskPass : ScriptableRenderPass
        {
            private readonly Material m_Material;
            private readonly ProfilingSampler m_ProfilingSampler =
                new ProfilingSampler("Camera Arc Scene Mask");

            private RTHandle m_TemporaryColorTexture;

            public CameraArcSceneMaskPass(Material material)
            {
                m_Material = material;
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            public override void OnCameraSetup(
                CommandBuffer cmd,
                ref RenderingData renderingData)
            {
                RenderTextureDescriptor descriptor =
                    renderingData.cameraData.cameraTargetDescriptor;

                descriptor.depthBufferBits = 0;
                descriptor.msaaSamples = 1;

                RenderingUtils.ReAllocateIfNeeded(
                    ref m_TemporaryColorTexture,
                    descriptor,
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp,
                    name: "_CameraArcSceneMaskTemporaryColor"
                );
            }

            public override void Execute(
                ScriptableRenderContext context,
                ref RenderingData renderingData)
            {
                if (m_Material == null)
                {
                    return;
                }

                CommandBuffer cmd =
                    CommandBufferPool.Get("Camera Arc Scene Mask");

                using (new ProfilingScope(cmd, m_ProfilingSampler))
                {
                    RTHandle cameraColorTarget =
                        renderingData.cameraData.renderer.cameraColorTargetHandle;

                    Blitter.BlitCameraTexture(
                        cmd,
                        cameraColorTarget,
                        m_TemporaryColorTexture
                    );

                    Blitter.BlitCameraTexture(
                        cmd,
                        m_TemporaryColorTexture,
                        cameraColorTarget,
                        m_Material,
                        0
                    );
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public void Dispose()
            {
                if (m_TemporaryColorTexture != null)
                {
                    m_TemporaryColorTexture.Release();
                    m_TemporaryColorTexture = null;
                }
            }
        }
    }
}
