using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

internal static class MediumDataUpdater
{
    private static MediumData[] _mediums;
    private static ComputeBuffer _mediumsBuffer;

    private static HashSet<Material> _matsInternal;



    private static MediumData GetMaterialData(Material mat)
    {
        float aniso = mat.HasFloat("_AnisotropyVolume") ? mat.GetFloat("_AnisotropyVolume") : 0;
        float scatter = mat.HasFloat("_TransmissiveScattering") ? mat.GetFloat("_TransmissiveScattering") : 100;
        bool homogeneous = mat.HasFloat("_Homogeneous") ? mat.GetFloat("_Homogeneous") > 0 : true; 

        float density = mat.HasFloat("_Density") ? mat.GetFloat("_Density") : 1;
        float absorption = mat.HasFloat("_TransmissiveAbsorption") ? mat.GetFloat("_TransmissiveAbsorption") : 0.1f;

        Color colTemp = mat.GetColor("_TransmissiveColor");
        float3 col = new(colTemp.r, colTemp.g, colTemp.b);

        Color emissionTemp = mat.GetColor("_EmissionTint");
        float3 emission = new(emissionTemp.r, emissionTemp.g, emissionTemp.b);


        MediumData data = new()
        {
            mediumColor = col * density * absorption,
            mediumEmission = emission * density,
            mediumAnisotropy = aniso,
            mediumScatteringCoeff = scatter * density,
            mediumHomogeneous = homogeneous ? 1u : 0u
        };

        return data;
    }

    /// <summary>
    /// Scans for materials and initializes values
    /// </summary>
    public static void Initialize()
    {
        var renderers = Object.FindObjectsOfType<MeshRenderer>();
        _matsInternal = new();

        // Gather all materials
        int matCounter = 1;
        for (int i = 0; i < renderers.Length; i++)
        {
            var matsTemp = renderers[i].sharedMaterials;

            for (int m = 0; m < matsTemp.Length; m++)
            {
                Material mat = matsTemp[m];
                if (mat.HasInteger("_MediumId") && !_matsInternal.Contains(mat))
                {
                    _matsInternal.Add(mat);
                    mat.SetInteger("_MediumId", matCounter);
                    matCounter++;
                }
            }
        }

        // Create mat data
        _mediums = new MediumData[_matsInternal.Count + 1];
        _mediumsBuffer = new ComputeBuffer(_matsInternal.Count + 1, Marshal.SizeOf<MediumData>());

        // First material - air
        MediumData airMaterial = new()
        {
            mediumColor = new Unity.Mathematics.float3(0, 0, 0),
            mediumEmission = new Unity.Mathematics.float3(0, 0, 0),
            mediumScatteringCoeff = 0,
            mediumAnisotropy = 0,
            mediumHomogeneous = 1
        };
        _mediums[0] = airMaterial;

        int counter = 1;
        foreach (var mat in _matsInternal)
        {
            MediumData data = GetMaterialData(mat);

            _mediums[counter] = data;
            counter++;
        }

        _mediumsBuffer.SetData(_mediums);
    }

    /// <summary>
    /// Updates material properties
    /// </summary>
    public static void UpdateMaterials()
    {
        int counter = 1;
        foreach (var mat in _matsInternal)
        {
            MediumData data = GetMaterialData(mat);

            _mediums[counter] = data;
            counter++;
        }
        _mediumsBuffer.SetData(_mediums);

        Shader.SetGlobalBuffer("gMediums", _mediumsBuffer);
    }

    public static void Dispose()
    {
        _mediumsBuffer.Dispose();
    }
}
