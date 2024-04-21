Shader "BeeTrace/SurfaceDisney"
{
    Properties
    {
        _AlbedoTint ("Color", Color) = (1,1,1,1)
        _AlbedoTex ("Albedo (RGB)", 2D) = "white" {}

        [Toggle]_USE_MASK_MAP("Use Mask maps (HDRP)", float) = 0
        _MaskTex ("Mask Map", 2D) = "black" {}

        _RoughnessBase ("Roughness", Range(0,1)) = 0.5
        _RoughnessTex ("Roughness Map", 2D) = "white" {}
        _RoughnessMinMax ("Roughness Remap", Vector) = (0, 1, 0, 0)

        _MetallicBase ("Metallic", Range(0,1)) = 0.0
        _MetallicTex ("Metallic Map", 2D) = "white"{}
        _MetallicMinMax ("Metallic Remap", Vector) = (0, 1, 0, 0)

        _NormalTex ("Normal Map", 2D) = "bump"{}
        _NormalStrength("Normal Strength", Range(0, 2)) = 1


        [Toggle]_Emission("Emission", float) = 0
        _EmissionTex ("Emission", 2D) = "black" {}
        [HDR]_EmissionTint("Emission Tint", Color) = (1,1,1)

        [Space]
        _Specular("Specular Value (Blender)", Range(0.0, 1.5)) = 0.5
        _IOR("Index of Refraction", Range(1.0, 2.8)) = 1.5

        [Space]
        _Anisotropy("Anisotropy", Range(0, 1)) = 0

        [Space]
        _Sheen("Sheen", Range(0, 1)) = 0
        _SheenTint("SheenTint", Range(0, 1)) = 0

        [Space]
        _Clearcoat("Clearcoat", Range(0, 1)) = 0
        _ClearcoatGloss("Clearcoat Glossiness", Range(0, 1)) = 0

        [Space]

        _Transmission("Transmission", Range(0, 1)) = 0
        _TransmissionSpecularity("Transmission Specularity", Range(0, 1)) = 0
        _TransmissiveColor ("Transmission Color", Color) = (1,1,1,1)
        [PowerSlider (3)] _TransmissiveScattering ("Transmission Scattering", Range(0, 1000)) = 0


        [Space]
        _SpecularTint("Specular Tint (Non-PBR)", Range(0, 1)) = 0
        _Thin("Thin (No volume)", Range(0, 1)) = 0
        _Flatness("Flatness", Range(0, 1)) = 0

        [Space]
        _MediumId("Medium ID Debug Only", Integer) = 0


    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        sampler2D _AlbedoTex;
        sampler2D _RoughnessTex;
        sampler2D _MaskTex;
        sampler2D _MetallicTex;
        sampler2D _NormalTex;
        sampler2D _EmissionTex;

        #pragma multi_compile __ _USE_MASK_MAP

        #pragma multi_compile __ _ALBEDO_MAP_ASSIGNED
        #pragma multi_compile __ _NORMAL_MAP_ASSIGNED
        #pragma multi_compile __ _MASK_MAP_ASSIGNED
        #pragma multi_compile __ _ROUGHNESS_MAP_ASSIGNED
        #pragma multi_compile __ _METALLIC_MAP_ASSIGNED
        #pragma multi_compile __ _EMISSION_MAP_ASSIGNED

        struct Input
        {
            float2 uv_AlbedoTex;
            float2 uv_MaskTex;
            float2 uv_RoughnessTex;
            float2 uv_MetallicTex;
            float2 uv_NormalTex;
            float2 uv_EmissionTex;
        };

        float _RoughnessBase;
        float2 _RoughnessMinMax;

        float _MetallicBase;
        float2 _MetallicMinMax;

        float4 _AlbedoTint;
        float4 _EmissionTint;
        float _NormalStrength;

        float map(float value, float min1, float max1, float min2, float max2)
        {
            // Convert the current value to a percentage
            // 0% - min1, 100% - max1
            float perc = (value - min1) / (max1 - min1);

            // Do the same operation backwards with min2 and max2
            float val = perc * (max2 - min2) + min2;
            return val;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
#if _ALBEDO_MAP_ASSIGNED
            float4 c = tex2D(_AlbedoTex, IN.uv_AlbedoTex) * _AlbedoTint;
#else
            float4 c = _AlbedoTint;
#endif
            o.Albedo = c.rgb;

#if _USE_MASK_MAP
    #if _MASK_MAP_ASSIGNED
                float4 maskVal = tex2D(_MaskTex, IN.uv_MaskTex);
                float rough = 1 - maskVal.w;
                float metal = maskVal.x;

                rough = map(rough, 0, 1, _RoughnessMinMax.x, _RoughnessMinMax.y);
                metal = map(metal, 0, 1, _MetallicMinMax.x, _MetallicMinMax.y);
    #else
                float rough = _RoughnessBase;
                float metal = _MetallicBase;
    #endif

#else // NO MASK MAP

    #if _ROUGHNESS_MAP_ASSIGNED
                float rough = tex2D(_RoughnessTex, IN.uv_RoughnessTex);
                rough = map(rough, 0, 1, _RoughnessMinMax.x, _RoughnessMinMax.y);
    #else
                float rough = _RoughnessBase;
    #endif


    #if _METALLIC_MAP_ASSIGNED
                float metal = tex2D(_MetallicTex, IN.uv_MetallicTex);
                metal = map(metal, 0, 1, _MetallicMinMax.x, _MetallicMinMax.y);
    #else
                float metal = _MetallicBase;
    #endif
#endif // NO MASK MAP END

            o.Smoothness = 1 - rough;
            o.Metallic = metal;


#if _NORMAL_MAP_ASSIGNED
            float3 norm = UnpackNormal(tex2D(_NormalTex, IN.uv_NormalTex));

            norm = lerp(float3(0, 0, 1), norm, _NormalStrength);
            o.Normal = norm;
#endif

#if _EMISSION_MAP_ASSIGNED
    float3 emission = tex2D(_EmissionTex, IN.uv_EmissionTex);
    o.Emission = emission * _EmissionTint;
#endif
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"

    SubShader
    {
        Pass
        {
            Name "PathTracing"
            Tags{ "LightMode" = "RayTracing" }

            HLSLPROGRAM

            #include "UnityRaytracingMeshUtils.cginc"
            #include "Core/PTData.cginc"
            #include "Core/PTGlobals.cginc"
            #include "Core/PTUtils.cginc"
            #include "Helper/PTVolumetrics.cginc"

            #pragma raytracing test

            #pragma multi_compile __ _USE_MASK_MAP

            #pragma shader_feature_raytracing _EMISSION
            #pragma multi_compile __ _ALBEDO_MAP_ASSIGNED
            #pragma multi_compile __ _MASK_MAP_ASSIGNED
            #pragma multi_compile __ _NORMAL_MAP_ASSIGNED
            #pragma multi_compile __ _ROUGHNESS_MAP_ASSIGNED
            #pragma multi_compile __ _METALLIC_MAP_ASSIGNED
            #pragma multi_compile __ _EMISSION_MAP_ASSIGNED



            Texture2D<float4> _AlbedoTex;
            SamplerState sampler__AlbedoTex;
            float4 _AlbedoTex_ST;
            float4 _AlbedoTint;

            Texture2D<float4> _MaskTex;
            SamplerState sampler__MaskTex;
            float4 _MaskTex_ST;

            Texture2D<float> _RoughnessTex;
            SamplerState sampler__RoughnessTex;
            float4 _RoughnessTex_ST;
            float2 _RoughnessMinMax;
            float _RoughnessBase;

            Texture2D<float> _MetallicTex;
            SamplerState sampler__MetallicTex;
            float4 _MetallicTex_ST;
            float2 _MetallicMinMax;
            float _MetallicBase;

            Texture2D<float4> _NormalTex;
            SamplerState sampler__NormalTex;
            float4 _NormalTex_ST;
            float _NormalStrength;

            Texture2D<float4> _EmissionTex;
            SamplerState sampler__EmissionTex;
            float4 _EmissionTex_ST;
            float4 _EmissionTint;

            float _Roughness;
            float _Metallic;

            float _Anisotropy;
            float _Specular;

            float _Transmission;
            float4 _TransmissiveColor;
            float _TransmissionSpecularity;
            float _TransmissiveScattering;

            float _Clearcoat;
            float _ClearcoatGloss;

            float _Sheen;
            float _SheenTint;

            float _SpecularTint;
            float _Thin;
            float _Flatness;

            int _MediumId;

            #include "Disney/PTDisneyUtils.cginc"
            #include "Disney/PTDisneyLogic.cginc"
            #include "Helper/RTBoilerplate.cginc"



            [shader("closesthit")]
            void ClosestHitMain(inout RayPayload payload : SV_RayPayload, AttributeData attribs : SV_IntersectionAttributes)
            {
                // Get surface data
                Vertex v;
                float3 hitObject, normalObject;
                bool isFrontFace;
                GetHitPosNormals(attribs, v, hitObject, normalObject, isFrontFace);

                float3 hitWorld = mul(ObjectToWorld(), float4(v.position, 1)).xyz;
                float3 normWorld = normalize(mul(normalObject, (float3x3)WorldToObject())).xyz;

                if (!isFrontFace && _Thin)
                {
                    normWorld = -normWorld;
                }

                // Decode normal map and modify world-space normal
                #ifdef _NORMAL_MAP_ASSIGNED
                
                    float3 normalMap = _NormalTex.SampleLevel(sampler__NormalTex, v.uv * _NormalTex_ST.xy + _NormalTex_ST.zw, 0);
                    float3 tangentWorld = normalize(mul(v.tangent, WorldToObject()));

                    DecodeNormalMap(normalMap, normWorld, tangentWorld, _NormalStrength);
                #endif


                // Sample surface at hit point
                MaterialData matData = InitializeSurface(v.uv);
                
                // Stochastic opacity sample - if alpha is non-1, randomise a chance to pass through unaffected
                if(RandomFloat01(payload.rngState) > matData.surfaceColor.w)
                {
                    payload.bounceRayOrigin = hitWorld + (WorldRayDirection() * K_RAY_ORIGIN_OFFSET * 0.1);
                    payload.bounceRayDirection = WorldRayDirection();
                    payload.t = RayTCurrent();
                    payload.albedo = float3(1,1,1);

                    CalculateVolumetrics(payload);

                    return;
                }

                float3x3 tnb = GetTangentSpace(normWorld);

                // Relative ior
                float3 wo = ToLocal(tnb, -WorldRayDirection());
                float ni = wo.y > 0.0f ? 1.0f : matData.ior;
                float nt = wo.y > 0.0f ? matData.ior : 1.0f;
                matData.relativeIor = ni / nt;


                // Sample Disney shader
                BsdfSample bsdfSample = InitNewSample();
                bool refracted = SampleDisney(payload, matData, -WorldRayDirection(), tnb, bsdfSample);

                // Set output parameters
                payload.albedo = bsdfSample.reflectance;
                #if _EMISSION
                    payload.emission = matData.emission;
                #endif
                payload.isFrontFace = isFrontFace;

                payload.bounceRayOrigin = hitWorld + (normWorld * K_RAY_ORIGIN_OFFSET);
                payload.bounceRayDirection = bsdfSample.wi;
                payload.depthSurface = payload.depthSurface + 1;
                payload.t = RayTCurrent();
                
                // Initialize denoising channels if not already initialized
                if(all(payload.denoisingAlbedo == -1))
                    payload.denoisingAlbedo = matData.surfaceColor;

                if(all(payload.denoisingNormal == -2))
                    payload.denoisingNormal = normWorld;


                // Process volumetric scattering + absorption
                CalculateVolumetrics(payload);

                // Do not modify material stack if scattered in volume, as the ray has not actually "hit" the triangle (got scattered before)
                if(!payload.wasScattered && matData.thin == 0 && refracted)
                {
                    // Material entry/exit depends entirely on triangle winding
                    // Note that this means correct orientation is required
                    if(isFrontFace)
                    {
                        AddMaterialToStack(payload, _MediumId);
                    }
                    else
                    {
                        PopMaterialFromStack(payload);
                    } 
                }
            }
            ENDHLSL
        }
    }
    CustomEditor "PTDisneyGUI"
}
