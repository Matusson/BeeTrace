using System.Runtime.InteropServices;
using UnityEngine;

[System.Serializable]
[StructLayout(LayoutKind.Sequential)]
public struct PathTracingSettings
{
    [Tooltip("Refers to how many rendering iterations will be performed between updating the viewport." +
        " Higher values mean better GPU utilization and thus completion speed, but may be unresponsive." +
        " Too high may crash the GPU driver.")]
    public int samplesPerBatch;

    [Header("Bounces")]
    [Tooltip("Light will perform at most this many bounces around surfaces.")]
    public int maxBouncesSurface;
    [Tooltip("Light will perform at most this many bounces around volumes")]
    public int maxBouncesVolume;

    [Header("Volumetrics")]
    [Tooltip("Volumetrics processing can be disabled for a speed boost. Note that absorption in glass and SSS is also handled as volumetrics.")]
    public bool enableVolumetrics;
    [Tooltip("Unless interrupted, this many steps will be performed while raymarching every volume.")]
    public int raymarchingSteps;
    [Tooltip("Minimum bound for raymarching step size. Step size is driven by step count, " +
        "but setting this value can prevent small intersections generating lots of steps.")]
    public float minStepSize;

    [Tooltip("Additional darkening effect. Physically-correct value is 0, but higher values can look subjectively better.")]
    public float extinctionBoost;

    [Header("Misc")]
    [Tooltip("Modulates jitter applied to each pixel. 1.0 results in perfectly sharp images, higher values smooth out the image slightly.")]
    public float filterSize;
    [Tooltip("Clamps radiance to this value. Lowering results in darker images, but can help eliminate fireflies.")]
    public float radianceClamp;
}

