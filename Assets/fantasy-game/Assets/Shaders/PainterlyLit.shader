Shader "FantasyGame/PainterlyLit"
{
    Properties
    {
        _BaseColor ("Base Color Tint", Color) = (1, 1, 1, 1)
        _ShadowColor ("Shadow Tint", Color) = (0.3, 0.25, 0.45, 1)
        _BandCount ("Light Bands", Range(2, 8)) = 3
        _BandSmoothness ("Band Smoothness", Range(0, 0.5)) = 0.05
        _RimPower ("Rim Power", Range(1, 8)) = 3
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.4
        _RimColor ("Rim Color", Color) = (1, 0.9, 0.7, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        // ============================================
        // FORWARD LIT PASS
        // ============================================
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
                float4 vertexColor : COLOR;
                float fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _ShadowColor;
                float _BandCount;
                float _BandSmoothness;
                float _RimPower;
                float _RimStrength;
                float4 _RimColor;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(posInputs.positionWS);
                output.vertexColor = input.color;
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);

                return output;
            }

            // Banded/stepped lighting for painterly look
            float BandedDiffuse(float NdotL, float bands, float smoothness)
            {
                float stepped = floor(NdotL * bands) / bands;
                // Smooth the band edges slightly to avoid harsh aliasing
                float nextStep = ceil(NdotL * bands) / bands;
                float f = frac(NdotL * bands);
                float blend = smoothstep(0.5 - smoothness, 0.5 + smoothness, f);
                return lerp(stepped, nextStep, blend);
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);

                // Get main directional light
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                // Base albedo from vertex color * tint
                float3 albedo = input.vertexColor.rgb * _BaseColor.rgb;

                // Diffuse with banding
                float NdotL = saturate(dot(normalWS, mainLight.direction));
                float banded = BandedDiffuse(NdotL, _BandCount, _BandSmoothness);

                // Apply shadow attenuation to the banded light
                float shadow = mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                banded *= shadow;

                // Lerp between shadow color and lit color
                float3 litColor = albedo * mainLight.color;
                float3 shadowColor = albedo * _ShadowColor.rgb;
                float3 diffuse = lerp(shadowColor, litColor, banded);

                // Add contribution from additional lights (unbanded, simpler)
                int additionalLightCount = GetAdditionalLightsCount();
                for (int i = 0; i < additionalLightCount; i++)
                {
                    Light addLight = GetAdditionalLight(i, input.positionWS);
                    float addNdotL = saturate(dot(normalWS, addLight.direction));
                    float addAtten = addLight.shadowAttenuation * addLight.distanceAttenuation;
                    diffuse += albedo * addLight.color * addNdotL * addAtten * 0.5;
                }

                // Ambient / environment light
                float3 ambient = SampleSH(normalWS) * albedo * 0.3;
                diffuse += ambient;

                // Fresnel rim lighting
                float rim = pow(1.0 - saturate(dot(viewDirWS, normalWS)), _RimPower);
                diffuse += rim * _RimColor.rgb * _RimStrength;

                // Fog
                float3 finalColor = MixFog(diffuse, input.fogFactor);

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }

        // ============================================
        // SHADOW CASTER PASS
        // ============================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float3 _LightDirection;

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                posWS = ApplyShadowBias(posWS, normalWS, _LightDirection);
                output.positionCS = TransformWorldToHClip(posWS);

                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // ============================================
        // DEPTH ONLY PASS
        // ============================================
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Simple Lit"
}
