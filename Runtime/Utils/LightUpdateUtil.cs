using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Utility class for updating light data buffers.
/// </summary>
public static class LightUpdateUtil
{
    private static PointLightData[] _pointData;
    private static List<Light> _pointLights;

    private static SpotLightData[] _spotData;
    private static List<Light> _spotLights;

    public static void UpdateLights(ref ComputeBuffer pointLightBuffer, ref ComputeBuffer spotLightBuffer)
    {
        _pointLights = new();
        _spotLights = new();


        // Classify light types
        var allLights = Object.FindObjectsOfType<Light>(false);
        for (int i = 0; i < allLights.Length; i++)
        {
            var light = allLights[i];
            if (light.type == UnityEngine.LightType.Point)
                _pointLights.Add(light);

            if (light.type == UnityEngine.LightType.Spot)
                _spotLights.Add(light);
        }

        UpdatePoints(ref pointLightBuffer);
        UpdateSpots(ref spotLightBuffer);
    }

    private static void UpdatePoints(ref ComputeBuffer pointLightBuffer)
    {
        // Reallocate if added new or deleted
        int pointCount = _pointLights.Count;
        if (pointCount == 0)
            pointCount = 1;

        if (_pointData == null || _pointData.Length != pointCount)
        {
            if (pointLightBuffer != null)
                pointLightBuffer.Release();

            pointLightBuffer = new ComputeBuffer(pointCount, PointLightData.GetByteSize(), ComputeBufferType.Default);

            _pointData = new PointLightData[pointCount];
        }

        // Read light data
        // Backup for case with no lights (can't create 0-length buffer)
        if (_pointLights.Count == 0)
        {
            PointLightData lightData = new()
            {
                position = new float3(),
                emission = new float3(),
                radius = 0
            };
            _pointData[0] = lightData;
        }
        else
        {
            for (int i = 0; i < _pointLights.Count; i++)
            {
                Light thisLight = _pointLights[i];
                float3 color = new(thisLight.color.r, thisLight.color.g, thisLight.color.b);
                float radius = 0.1f;

                if (thisLight.TryGetComponent(out BeeTraceLight ptLight))
                {
                    radius = ptLight.radius;
                }


                PointLightData lightData = new()
                {
                    position = thisLight.transform.position,
                    emission = color * thisLight.intensity,
                    radius = radius
                };
                _pointData[i] = lightData;
            }
        }
        pointLightBuffer.SetData(_pointData);
    }

    private static void UpdateSpots(ref ComputeBuffer spotLightBuffer)
    {
        // Reallocate if added new or deleted
        int spotCount = _spotLights.Count;
        if (spotCount == 0)
            spotCount = 1;

        if (_spotData == null || _spotData.Length != spotCount)
        {
            if (spotLightBuffer != null)
                spotLightBuffer.Release();

            spotLightBuffer = new ComputeBuffer(spotCount, SpotLightData.GetByteSize(), ComputeBufferType.Default);

            _spotData = new SpotLightData[spotCount];
        }

        // Read light data
        // Backup for case with no lights (can't create 0-length buffer)
        if (_spotLights.Count == 0)
        {
            SpotLightData lightData = new()
            {
                position = new float3(),
                dir = new float3(),
                emission = new float3(),
                angle = 0,
                radius = 0
            };
            _spotData[0] = lightData;
        }
        else
        {
            for (int i = 0; i < _spotLights.Count; i++)
            {
                Light thisLight = _spotLights[i];
                float3 color = new(thisLight.color.r, thisLight.color.g, thisLight.color.b);
                float radius = 0.1f;

                if (thisLight.TryGetComponent(out BeeTraceLight ptLight))
                {
                    radius = ptLight.radius;
                }

                SpotLightData lightData = new()
                {
                    position = thisLight.transform.position,
                    dir = thisLight.transform.forward,
                    emission = color * thisLight.intensity,
                    angle = math.radians(thisLight.spotAngle) / 2,
                    radius = radius
                };
                _spotData[i] = lightData;
            }
        }
        spotLightBuffer.SetData(_spotData);
    }

    public static void Dispose()
    {
        _pointData = null;
        _pointLights.Clear();

        _spotData = null;
        _spotLights.Clear();
    }

    public struct PointLightData
    {
        public float3 position;
        public float radius;

        public float3 emission;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetByteSize()
        {
            return (3 * 4) * 2 + 4;
        }
    };

    public struct SpotLightData
    {
        public float3 position;
        public float3 dir;
        public float angle;
        public float radius;

        public float3 emission;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetByteSize()
        {
            return (3 * 4) * 3 + (4 * 2);
        }
    };
}
