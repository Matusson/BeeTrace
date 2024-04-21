using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace PT.Denoising
{
    public class Denoising : BasePostProcess
    {
        public int minSample = 1;
        public bool highQuality = true;
        public bool useAuxiliary = true;

        private IDenoiser _denoiser;
        private DenoisingDevice _device;

        private NativeArray<float4> _colors;
        private NativeArray<float4> _albedo;
        private NativeArray<float4> _normals;


        private void Start() { }

        public override int GetPriority()
        {
            return -100;
        }

        private void Setup(int2 dimensions)
        {
            if (_denoiser != null)
            {
                _denoiser.Dispose();
            }

            _denoiser = new OIDNDenoiser();
            _device = _denoiser.GetBestDevice();

            _denoiser.Initialize(_device, dimensions, highQuality, useAuxiliary);
        }

        public override void Process(RenderTexture source, RenderTexture dest)
        {

            //if (camManager.minStep)

            // Check if needs to reallocate
            int2 dimensions = new(source.width, source.height);
            bool resized = _denoiser == null || math.any(_denoiser.Dimensions != dimensions);

            if (_denoiser == null
                || resized
                || _denoiser.HighQuality != highQuality
                || _denoiser.UseAuxiliary != useAuxiliary)
            {
                Setup(new int2(source.width, source.height));
            }

            if (!_colors.IsCreated || resized)
            {
                if (_colors.IsCreated)
                    _colors.Dispose();

                _colors = new(source.width * source.height, Allocator.Persistent);
            }

            if (useAuxiliary && (!_albedo.IsCreated || resized))
            {
                if (_albedo.IsCreated)
                    _albedo.Dispose();

                if (_normals.IsCreated)
                    _normals.Dispose();

                _albedo = new(source.width * source.height, Allocator.Persistent);
                _normals = new(source.width * source.height, Allocator.Persistent);
            }

            // Read the render texture
            var asyncActionCol = AsyncGPUReadback.RequestIntoNativeArray(ref _colors, source, 0);
            if (useAuxiliary)
            {
                var camManager = GetComponent<BeeTraceCamera>();

                var asyncActionAlb = AsyncGPUReadback.RequestIntoNativeArray(ref _albedo, camManager._albedoTexture, 0);
                var asyncActionNorm = AsyncGPUReadback.RequestIntoNativeArray(ref _normals, camManager._normalTexture, 0);

                asyncActionAlb.WaitForCompletion();
                asyncActionNorm.WaitForCompletion();
            }

            asyncActionCol.WaitForCompletion();

            _denoiser.Denoise(_colors, _albedo, _normals);
            Texture2D testTex = new(source.width, source.height, TextureFormat.RGBAFloat, false);
            testTex.SetPixelData(_colors, 0);
            testTex.Apply();

            Graphics.Blit(testTex, dest);
            DestroyImmediate(testTex);
        }

        private void OnDestroy()
        {
            _denoiser?.Dispose();

            _colors.Dispose();
            if (_albedo.IsCreated)
            {
                _albedo.Dispose();
                _normals.Dispose();
            }
        }
    }
}