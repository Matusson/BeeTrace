Shader "BeeTrace/Volumetric"
{
    Properties
    {
        _TransmissiveColor ("Absorption Color", Color) = (1,1,1,1)
        [HDR] _EmissionTint ("Emission", Color) = (0,0,0)
        _Density("Density", float) = 1
        
        _TransmissiveScattering("Scattering", float) = 1
        _TransmissiveAbsorption("Absorption", float) = 1

        _AnisotropyVolume("Anisotropy", float) = 0

        [Space]
        _MediumId("Medium ID Debug Only", Integer) = 0

    }
    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType"="Transparent" } 
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows alpha:fade

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
        };

        half _Roughness;
        half _Metallic;
        fixed4 _Color;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Albedo comes from a texture tinted by color
            fixed4 c = _Color;
            o.Albedo = c.rgb;

            // Metallic and smoothness come from slider variables
            o.Metallic = _Metallic;
            o.Smoothness = 1 - _Roughness;
            o.Alpha = 0.2;
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

            float3 _TransmissiveColor;
            float3 _EmissionTint;
            float _Density;

            float _TransmissiveScattering;
            float _TransmissiveAbsorption;

            int _MediumId;
            float _AnisotropyVolume;

            //float _Homogeneous;


            #include "Helper/RTBoilerplate.cginc"

            [shader("closesthit")]
            void ClosestHitMain(inout RayPayload payload : SV_RayPayload, AttributeData attribs : SV_IntersectionAttributes)
            {
                uint3 triangleIndices = UnityRayTracingFetchTriangleIndices(PrimitiveIndex());

                Vertex v0, v1, v2;
                v0 = FetchVertex(triangleIndices.x);
                v1 = FetchVertex(triangleIndices.y);
                v2 = FetchVertex(triangleIndices.z);

                float3 barycentricCoords = float3(1.0 - attribs.barycentrics.x - attribs.barycentrics.y, attribs.barycentrics.x, attribs.barycentrics.y);
                Vertex v = InterpolateVertices(v0, v1, v2, barycentricCoords);

                bool isFrontFace = HitKind() == HIT_KIND_TRIANGLE_FRONT_FACE;

                // Get surface data
                float3 hitPos = v.position;
                float3 geoNorm = v.normal;

                // World-space transformation
                float3 hitWorld = mul(ObjectToWorld(), float4(v.position, 1)).xyz;
                float3 normWorld = normalize(mul(geoNorm, (float3x3)WorldToObject()));

                // Set output parameters
                payload.albedo = float3(1,1,1);
                payload.emission = float3(0,0,0);

                // Volumetrics can affect denoising above a given threshold
                if(all(payload.denoisingAlbedo == -1) && _Density > MIN_DENSITY_FOR_DENOISING)
                    payload.denoisingAlbedo = _TransmissiveColor.xyz;

                payload.isFrontFace = isFrontFace;

                payload.bounceRayOrigin = hitWorld;
                payload.bounceRayDirection = WorldRayDirection();

                // Unused for heterogeneous rendering, see note in PTVolumetrics.cginc
                //#undef PT_SAMPLE_VOLUME_DENSITY;
                //#define PT_SAMPLE_VOLUME_DENSITY(worldPos) _DensityTex.SampleLevel(sampler__DensityTex, worldPos), 0)

                //#undef PT_SAMPLE_VOLUME_COLOR;
                //#define PT_SAMPLE_VOLUME_COLOR(worldPos) clamp(worldPos, 0, 1)

                //#undef PT_SAMPLE_VOLUME_EMISSION;
                //#define PT_SAMPLE_VOLUME_EMISSION(worldPos) _FlameTex.SampleLevel(sampler__FlameTex, worldPos * float3(1, 0.5, 1) + float3(0,0.11,0), 0)

                //#undef PT_SAMPLE_VOLUME_DENSITY;
                CalculateVolumetrics(payload);

                if(!payload.wasScattered)
                {
                    if(isFrontFace)
                        AddMaterialToStack(payload, _MediumId);
                    else
                        PopMaterialFromStack(payload);
                }


            }
            ENDHLSL
        }
    }
}
