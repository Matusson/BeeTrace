using System;
using Unity.Collections;
using Unity.Mathematics;

namespace PT.Denoising
{
    public interface IDenoiser : IDisposable
    {
        public int2 Dimensions { get; }

        public bool HighQuality { get; }

        public bool UseAuxiliary { get; }


        /// <summary>
        /// Returns all devices supported by the denoiser.
        /// </summary>
        /// <returns></returns>
        public DenoisingDevice[] GetAvailableDevices();

        /// <summary>
        /// Autonomically picks a GPU device if available, otherwise picks a CPU device.
        /// </summary>
        public DenoisingDevice GetBestDevice();


        /// <summary>
        /// Set up the denoising logical device and initialize buffers.
        /// </summary>
        public void Initialize(DenoisingDevice device, int2 dimensions, bool highQuality, bool useAuxiliary);

        /// <summary>
        /// Perform denoising on a given image.
        /// </summary>
        public void Denoise(NativeArray<float4> beauty, NativeArray<float4>? albedo, NativeArray<float4>? normal);
    }
}