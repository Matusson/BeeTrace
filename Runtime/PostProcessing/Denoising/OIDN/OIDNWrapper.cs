using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace PT.Denoising
{
    internal static unsafe class OIDNWrapper
    {
        internal enum OIDNDeviceType
        {
            OIDN_DEVICE_TYPE_DEFAULT = 0, // select device automatically

            OIDN_DEVICE_TYPE_CPU = 1, // CPU device
            OIDN_DEVICE_TYPE_SYCL = 2, // SYCL device
            OIDN_DEVICE_TYPE_CUDA = 3, // CUDA device
            OIDN_DEVICE_TYPE_HIP = 4 // HIP device
        }
        internal enum OIDNError
        {
            OIDN_ERROR_NONE = 0, // no error occurred
            OIDN_ERROR_UNKNOWN = 1, // an unknown error occurred
            OIDN_ERROR_INVALID_ARGUMENT = 2, // an invalid argument was specified
            OIDN_ERROR_INVALID_OPERATION = 3, // the operation is not allowed
            OIDN_ERROR_OUT_OF_MEMORY = 4, // not enough memory to execute the operation
            OIDN_ERROR_UNSUPPORTED_HARDWARE = 5, // the hardware (e.g. CPU) is not supported
            OIDN_ERROR_CANCELLED = 6 // the operation was cancelled by the user
        }

        internal enum OIDNFormat
        {
            OIDN_FORMAT_UNDEFINED = 0,

            // 32-bit single-precision floating-point scalar and vector formats
            OIDN_FORMAT_FLOAT = 1,
            OIDN_FORMAT_FLOAT2,
            OIDN_FORMAT_FLOAT3,
            OIDN_FORMAT_FLOAT4,

            // 16-bit half-precision floating-point scalar and vector formats
            OIDN_FORMAT_HALF = 257,
            OIDN_FORMAT_HALF2,
            OIDN_FORMAT_HALF3,
            OIDN_FORMAT_HALF4,
        }

        internal enum OIDNQuality
        {
            OIDN_QUALITY_DEFAULT = 0, // default quality

            //OIDN_QUALITY_FAST     = 4
            OIDN_QUALITY_BALANCED = 5, // balanced quality/performance (for interactive/real-time rendering)
            OIDN_QUALITY_HIGH = 6, // high quality (for final-frame rendering)
        }

        internal struct OIDNDevice { }

        internal struct OIDNBuffer { }

        internal struct OIDNFilter { }



        [DllImport("OpenImageDenoise", CharSet = CharSet.Ansi)]
        internal static unsafe extern int oidnGetNumPhysicalDevices();

        // Devices

        [DllImport("OpenImageDenoise", CharSet = CharSet.Ansi)]
        internal static unsafe extern OIDNDevice* oidnNewDevice(OIDNDeviceType type);

        [DllImport("OpenImageDenoise", CharSet = CharSet.Ansi)]
        internal static unsafe extern OIDNDevice* oidnNewDeviceByID(int physicalDeviceID);

        [DllImport("OpenImageDenoise", CharSet = CharSet.Ansi)]
        internal static unsafe extern void oidnCommitDevice(OIDNDevice* device);


        [DllImport("OpenImageDenoise", CharSet = CharSet.Ansi)]
        internal static unsafe extern OIDNError oidnGetDeviceError(OIDNDevice* device, char** outMessage);

        [DllImport("OpenImageDenoise", CharSet = CharSet.Ansi)]
        internal static unsafe extern int oidnGetPhysicalDeviceInt(int physicalDeviceID, char* name);

        [DllImport("OpenImageDenoise", CharSet = CharSet.Ansi)]
        internal static unsafe extern char* oidnGetPhysicalDeviceString(int physicalDeviceID, char* name);

        [DllImport("OpenImageDenoise", CharSet = CharSet.Ansi)]
        internal static unsafe extern void oidnReleaseDevice(OIDNDevice* device);


        // Buffers 

        [DllImport("OpenImageDenoise", CharSet = CharSet.Ansi)]
        internal static unsafe extern OIDNBuffer* oidnNewBuffer(OIDNDevice* device, int byteSize);

        [DllImport("OpenImageDenoise", CharSet = CharSet.Ansi)]
        internal static unsafe extern void* oidnGetBufferData(OIDNBuffer* buffer);

        [DllImport("OpenImageDenoise", CharSet = CharSet.Ansi)]
        internal static unsafe extern void oidnReadBuffer(OIDNBuffer* buffer, int byteOffset, int byteSize, void* dstHostPtr);

        [DllImport("OpenImageDenoise", CharSet = CharSet.Ansi)]
        internal static unsafe extern void oidnWriteBuffer(OIDNBuffer* buffer,
                              int byteOffset, int byteSize, void* srcHostPtr);

        [DllImport("OpenImageDenoise", CharSet = CharSet.Ansi)]
        internal static unsafe extern void oidnReleaseBuffer(OIDNBuffer* buffer);

        // Filters

        [DllImport("OpenImageDenoise", CharSet = CharSet.Ansi)]
        internal static unsafe extern OIDNFilter* oidnNewFilter(OIDNDevice* device, char* type);

        [DllImport("OpenImageDenoise", CharSet = CharSet.Ansi)]
        internal static unsafe extern void oidnSetFilterImage(OIDNFilter* filter, char* name,
                                 OIDNBuffer* buffer, OIDNFormat format,
                                 int width, int height,
                                 int byteOffset,
                                 int pixelByteStride, int rowByteStride);

        [DllImport("OpenImageDenoise", CharSet = CharSet.Ansi)]
        internal static unsafe extern void oidnSetFilterBool(OIDNFilter* filter, char* name, bool value);

        [DllImport("OpenImageDenoise", CharSet = CharSet.Ansi)]
        internal static unsafe extern void oidnSetFilterInt(OIDNFilter* filter, char* name, int value);

        [DllImport("OpenImageDenoise", CharSet = CharSet.Ansi)]
        internal static unsafe extern void oidnCommitFilter(OIDNFilter* filter);

        [DllImport("OpenImageDenoise", CharSet = CharSet.Ansi)]
        internal static unsafe extern void oidnExecuteFilter(OIDNFilter* filter);

        [DllImport("OpenImageDenoise", CharSet = CharSet.Ansi)]
        internal static unsafe extern void oidnReleaseFilter(OIDNFilter* filter);


        /// <summary>
        /// If there was an error since the last check, prints the error and returns false.
        /// </summary>
        /// <returns></returns>
        internal static bool WasSuccessful(OIDNDevice* device)
        {
            IntPtr errPtr = Marshal.AllocHGlobal(IntPtr.Size);
            OIDNError err = oidnGetDeviceError(device, (char**)errPtr);

            // Read the error if not successful
            if (err != OIDNError.OIDN_ERROR_NONE)
            {
                IntPtr nativeErrorPtr = Marshal.ReadIntPtr(errPtr);
                string errorString = Marshal.PtrToStringAnsi(nativeErrorPtr);

                Debug.Log($"{err}: {errorString}");
                Marshal.FreeHGlobal(errPtr);
                return false;
            }

            Marshal.FreeHGlobal(errPtr);
            return true;
        }
    }
}