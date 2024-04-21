Shader "Hidden/Tonemap"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float exposure;
            float contrast;
            float brightness;
            float whiteness;

			#pragma multi_compile __ MODE_LINEAR
			#pragma multi_compile __ MODE_REINHARD
			#pragma multi_compile __ MODE_FILMIC
			#pragma multi_compile __ MODE_ACES
			#pragma multi_compile __ MODE_AGX
			#pragma multi_compile __ MODE_AGX_PUNCHY


            // Tonemapper code from Godot has been adapted for these

			// Linear
			float3 tonemap_linear(float3 color)
			{
				return clamp(color, 0, 1);
			}

			// Reinhard
			float3 tonemap_reinhard(float3 color, float white) 
			{
				return (white * color + color) / (color * white + white);
			}

			// Filmic
			float3 tonemap_filmic(float3 color, float white) 
			{
				// exposure bias: input scale (color *= bias, white *= bias) to make the brightness consistent with other tonemappers
				// also useful to scale the input to the range that the tonemapper is designed for (some require very high input values)
				// has no effect on the curve's general shape or visual properties
				const float exposure_bias = 2.0f;
				const float A = 0.22f * exposure_bias * exposure_bias; // bias baked into constants for performance
				const float B = 0.30f * exposure_bias;
				const float C = 0.10f;
				const float D = 0.20f;
				const float E = 0.01f;
				const float F = 0.30f;

				float3 color_tonemapped = ((color * (A * color + C * B) + D * E) / (color * (A * color + B) + D * F)) - E / F;
				float white_tonemapped = ((white * (A * white + C * B) + D * E) / (white * (A * white + B) + D * F)) - E / F;

				return color_tonemapped / white_tonemapped;
			}

            // ACES
            float3 tonemap_aces(float3 color, float white) 
            {
	            const float exposure_bias = 1.8f;
	            const float A = 0.0245786f;
	            const float B = 0.000090537f;
	            const float C = 0.983729f;
	            const float D = 0.432951f;
	            const float E = 0.238081f;

	            // Exposure bias baked into transform to save shader instructions. Equivalent to `color *= exposure_bias`
	            const float3x3 rgb_to_rrt = float3x3(
			            float3(0.59719f * exposure_bias, 0.35458f * exposure_bias, 0.04823f * exposure_bias),
			            float3(0.07600f * exposure_bias, 0.90834f * exposure_bias, 0.01566f * exposure_bias),
			            float3(0.02840f * exposure_bias, 0.13383f * exposure_bias, 0.83777f * exposure_bias));

	            const float3x3 odt_to_rgb = float3x3(
			            float3(1.60475f, -0.53108f, -0.07367f),
			            float3(-0.10208f, 1.10813f, -0.00605f),
			            float3(-0.00327f, -0.07276f, 1.07602f));

	            color = mul(rgb_to_rrt, color);
	            float3 color_tonemapped = (color * (color + A) - B) / (color * (C * color + D) + E);
	            color_tonemapped = mul(odt_to_rgb, color_tonemapped);

	            white *= exposure_bias;
	            float white_tonemapped = (white * (white + A) - B) / (white * (C * white + D) + E);

	            return color_tonemapped / white_tonemapped;
            }

            // AGX
			float3 agx_default_contrast_approx(float3 x) 
			{
				float3 x2 = x * x;
				float3 x4 = x2 * x2;

				return +15.5 * x4 * x2 - 40.14 * x4 * x + 31.96 * x4 - 6.868 * x2 * x + 0.4298 * x2 + 0.1191 * x - 0.00232;
			}

			float3 agx(float3 val, float white) 
			{
				const float3x3 agx_mat = float3x3(
						0.842479062253094, 0.0423282422610123, 0.0423756549057051,
						0.0784335999999992, 0.878468636469772, 0.0784336,
						0.0792237451477643, 0.0791661274605434, 0.879142973793104);

				const float min_ev = -12.47393f;
				float max_ev = log2(white);

				// Input transform (inset).
				val = mul(agx_mat, val);

				// Log2 space encoding.
				val = clamp(log2(val), min_ev, max_ev);
				val = (val - min_ev) / (max_ev - min_ev);

				// Apply sigmoid function approximation.
				val = agx_default_contrast_approx(val);

				return val;
			}

			float3 agx_eotf(float3 val) 
			{
				const float3x3 agx_mat_inv = float3x3(
						1.19687900512017, -0.0528968517574562, -0.0529716355144438,
						-0.0980208811401368, 1.15190312990417, -0.0980434501171241,
						-0.0990297440797205, -0.0989611768448433, 1.15107367264116);

				// Inverse input transform (outset).
				val = mul(agx_mat_inv, val);

				// sRGB IEC 61966-2-1 2.2 Exponent Reference EOTF Display
				// NOTE: We're linearizing the output here. Comment/adjust when
				// *not* using a sRGB render target.
				val = pow(val, float3(2.2, 2.2, 2.2));

				return val;
			}

			float3 agx_look_punchy(float3 val) 
			{
				const float3 lw = float3(0.2126, 0.7152, 0.0722);
				float luma = dot(val, lw);

				float3 offset = float3(0,0,0);
				float3 slope = float3(1,1,1);
				float3 power = float3(1.35, 1.35, 1.35);
				float sat = 1.4;

				// ASC CDL.
				val = pow(val * slope + offset, power);
				return luma + sat * (val - luma);
			}

			// Adapted from https://iolite-engine.com/blog_posts/minimal_agx_implementation
			float3 tonemap_agx(float3 color, float white, bool punchy) 
			{
				color = agx(color, white);
				if (punchy) {
					color = agx_look_punchy(color);
				}
				color = agx_eotf(color);
				return color;
			}

            sampler2D _MainTex;

            float4 frag (v2f i) : SV_Target
            {
                float4 col = tex2D(_MainTex, i.uv) * exposure;

                // Tonemapping
				float4 tonemapped = col;
				#if MODE_LINEAR
					tonemapped = float4(tonemap_linear(col.rgb), 1);
				#endif

				#if MODE_REINHARD
					tonemapped = float4(tonemap_reinhard(col.rgb, whiteness), 1);
				#endif

				#if MODE_FILMIC
					tonemapped = float4(tonemap_filmic(col.rgb, whiteness), 1);
				#endif

				#if MODE_ACES
					tonemapped = float4(tonemap_aces(col.rgb, whiteness), 1);
                #endif

				#if MODE_AGX
					tonemapped = float4(tonemap_agx(col.rgb, whiteness, false), 1);
                #endif

				#if MODE_AGX_PUNCHY
					tonemapped = float4(tonemap_agx(col.rgb, whiteness, true), 1);
                #endif

                // Contrast
                tonemapped.rgb = ((tonemapped.rgb - 0.5f) * max(contrast, 0)) + 0.5f;

                // Brightness
                tonemapped.rgb += brightness;
                return tonemapped;
            }
            ENDCG
        }
    }
}
