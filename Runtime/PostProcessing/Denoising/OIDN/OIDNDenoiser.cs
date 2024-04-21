using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using static PT.Denoising.OIDNWrapper;

namespace PT.Denoising
{
    public unsafe class OIDNDenoiser : IDenoiser
    {
        private OIDNDevice* device;
        
        private OIDNFilter* filter;
        private OIDNFilter* albedoPrefilter;
        private OIDNFilter* normalPrefilter;


        private OIDNBuffer* colorBuf;
        private OIDNBuffer* albedoBuf;
        private OIDNBuffer* normalBuf;


        private readonly int pixelSize = 4 * sizeof(float); // 4 float channels
        private int bufferSize;

        public int2 Dimensions { get => _dimensions; }
        private int2 _dimensions;

        public bool HighQuality { get => _highQuality; }
        private bool _highQuality;
        
        public bool UseAuxiliary { get => _useAuxiliary; }
        private bool _useAuxiliary;



        public DenoisingDevice[] GetAvailableDevices()
        {
            int count = oidnGetNumPhysicalDevices();

            IntPtr nameParam = Marshal.StringToHGlobalAnsi("name");
            IntPtr typeParam = Marshal.StringToHGlobalAnsi("type");

            DenoisingDevice[] available = new DenoisingDevice[count];

            for (int i = 0; i < count; i++)
            {
                char* namePtr = oidnGetPhysicalDeviceString(i, (char*)nameParam);
                string name = Marshal.PtrToStringAnsi((IntPtr)namePtr);

                OIDNDeviceType type = (OIDNDeviceType)oidnGetPhysicalDeviceInt(i, (char*)typeParam);
                bool isCpu = type == OIDNDeviceType.OIDN_DEVICE_TYPE_DEFAULT || type == OIDNDeviceType.OIDN_DEVICE_TYPE_CPU;

                DenoisingDevice thisDevice = new()
                {
                    id = i,
                    type = isCpu ? DenoisingDeviceType.CPU : DenoisingDeviceType.GPU,
                    name = name
                };
                available[i] = thisDevice;
            }

            Marshal.FreeHGlobal(nameParam);
            Marshal.FreeHGlobal(typeParam);
            return available;
        }

        public DenoisingDevice GetBestDevice()
        {
            var devices = GetAvailableDevices();

            // Find the first GPU device
            for (int i = 0; i < devices.Length; i++)
            {
                if (devices[i].type == DenoisingDeviceType.GPU)
                    return devices[i];
            }

            return devices[0];
        }

        public void Initialize(DenoisingDevice denoisingDevice, int2 dimensions, bool highQuality, bool useAuxiliary)
        {
            device = oidnNewDeviceByID(denoisingDevice.id);
            oidnCommitDevice(device);

            _highQuality = highQuality;
            _useAuxiliary = useAuxiliary;
            _dimensions = dimensions;


            bufferSize = pixelSize * dimensions.x * dimensions.y;
            colorBuf = oidnNewBuffer(device, bufferSize);
            if (useAuxiliary)
            {
                albedoBuf = oidnNewBuffer(device, bufferSize);
                normalBuf = oidnNewBuffer(device, bufferSize);
            }

            IntPtr rtFilterName = Marshal.StringToHGlobalAnsi("RT");
            IntPtr colBufferName = Marshal.StringToHGlobalAnsi("color");
            IntPtr albedoBufferName = Marshal.StringToHGlobalAnsi("albedo");
            IntPtr normalBufferName = Marshal.StringToHGlobalAnsi("normal");
            IntPtr outputBufferName = Marshal.StringToHGlobalAnsi("output");
            IntPtr hdrName = Marshal.StringToHGlobalAnsi("hdr");
            IntPtr qualityName = Marshal.StringToHGlobalAnsi("quality");



            // Create filters
            filter = oidnNewFilter(device, (char*)rtFilterName); // generic ray tracing filter

            oidnSetFilterImage(filter, (char*)colBufferName, colorBuf,
                OIDNFormat.OIDN_FORMAT_FLOAT3, dimensions.x, dimensions.y, 0, pixelSize, pixelSize * dimensions.x); // beauty

            if (useAuxiliary)
            {
                oidnSetFilterImage(filter, (char*)albedoBufferName, albedoBuf,
                    OIDNFormat.OIDN_FORMAT_FLOAT3, dimensions.x, dimensions.y, 0, pixelSize, pixelSize * dimensions.x); // aux albedo

                oidnSetFilterImage(filter, (char*)normalBufferName, normalBuf,
                    OIDNFormat.OIDN_FORMAT_FLOAT3, dimensions.x, dimensions.y, 0, pixelSize, pixelSize * dimensions.x); // aux normal
            }

            oidnSetFilterImage(filter, (char*)outputBufferName, colorBuf,
                OIDNFormat.OIDN_FORMAT_FLOAT3, dimensions.x, dimensions.y, 0, pixelSize, pixelSize * dimensions.x); // denoised beauty

            OIDNQuality targetQuality = highQuality ? OIDNQuality.OIDN_QUALITY_HIGH : OIDNQuality.OIDN_QUALITY_BALANCED;
            
            oidnSetFilterBool(filter, (char*)hdrName, true); // enable HDR
            oidnSetFilterInt(filter, (char*)qualityName, (int)targetQuality);

            oidnCommitFilter(filter);


            // Auxiliary prefiltering
            if(highQuality && useAuxiliary)
            {
                // Albedo
                albedoPrefilter = oidnNewFilter(device, (char*)rtFilterName);

                oidnSetFilterImage(albedoPrefilter, (char*)albedoBufferName, albedoBuf,
                    OIDNFormat.OIDN_FORMAT_FLOAT3, dimensions.x, dimensions.y, 0, pixelSize, pixelSize * dimensions.x); // aux albedo

                oidnSetFilterImage(albedoPrefilter, (char*)outputBufferName, albedoBuf,
                    OIDNFormat.OIDN_FORMAT_FLOAT3, dimensions.x, dimensions.y, 0, pixelSize, pixelSize * dimensions.x); // albedo output

                oidnCommitFilter(albedoPrefilter);

                // Normal
                normalPrefilter = oidnNewFilter(device, (char*)rtFilterName);

                oidnSetFilterImage(normalPrefilter, (char*)normalBufferName, normalBuf,
                    OIDNFormat.OIDN_FORMAT_FLOAT3, dimensions.x, dimensions.y, 0, pixelSize, pixelSize * dimensions.x); // aux albedo

                oidnSetFilterImage(normalPrefilter, (char*)outputBufferName, normalBuf,
                    OIDNFormat.OIDN_FORMAT_FLOAT3, dimensions.x, dimensions.y, 0, pixelSize, pixelSize * dimensions.x); // albedo output

                oidnCommitFilter(normalPrefilter);
            }

            // Free allocated memory for strings
            Marshal.FreeHGlobal(rtFilterName);
            Marshal.FreeHGlobal(colBufferName);
            Marshal.FreeHGlobal(albedoBufferName);
            Marshal.FreeHGlobal(normalBufferName);
            Marshal.FreeHGlobal(outputBufferName);
            Marshal.FreeHGlobal(hdrName);
            Marshal.FreeHGlobal(qualityName);
        }

        public void Denoise(NativeArray<float4> beauty, NativeArray<float4>? albedo, NativeArray<float4>? normal)
        {
            oidnWriteBuffer(colorBuf, 0, bufferSize, beauty.GetUnsafePtr());

            // Write aux buffers
            if(albedoBuf != null && albedo.HasValue)
                oidnWriteBuffer(albedoBuf, 0, bufferSize, albedo.Value.GetUnsafePtr());

            if (normalBuf != null && normal.HasValue)
                oidnWriteBuffer(normalBuf, 0, bufferSize, normal.Value.GetUnsafePtr());

            // Execute the denoising
            // Prefilters
            if(albedoPrefilter != null && normalPrefilter != null)
            {
                oidnExecuteFilter(albedoPrefilter);
                oidnExecuteFilter(normalPrefilter);
            }

            // Final denoising
            oidnExecuteFilter(filter);

            // Check for errors
            if (!WasSuccessful(device))
                return;

            oidnReadBuffer(colorBuf, 0, bufferSize, beauty.GetUnsafePtr());
        }


        public void Dispose()
        {
            oidnReleaseFilter(filter);
            oidnReleaseBuffer(colorBuf);
            oidnReleaseBuffer(albedoBuf);
            oidnReleaseBuffer(normalBuf);

            oidnReleaseDevice(device);
        }
    }
}
