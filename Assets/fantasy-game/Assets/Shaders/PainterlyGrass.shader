Shader "FantasyGame/PainterlyGrass"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.3, 0.5, 0.2, 1)
        _TipColor ("Tip Color", Color) = (0.5, 0.65, 0.25, 1)
        _ShadowColor ("Shadow Tint", Color) = (0.2, 0.22, 0.35, 1)
        _WindSpeed ("Wind Speed", Range(0, 5)) = 1.5
        _WindStrength ("Wind Strength", Range(0, 0.5)) = 0.15
        _AlphaCutoff ("Alpha Cutoff", Range(0, 1)) = 0.3
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"
        }

        Cull Off  // Grass visible from both sides

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _TipColor;
                float4 _ShadowColor;
                float _WindSpeed;
                float _WindStrength;
                float _AlphaCutoff;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 posOS = input.positionOS.xyz;

                // Wind animation: sway the top vertices (uv.y > 0.3)
                float windFactor = saturate(input.uv.y - 0.3) / 0.7;
                float3 worldPos = TransformObjectToWorld(posOS);
                float windPhase = worldPos.x * 1.5 + worldPos.z * 0.7 + _Time.y * _WindSpeed;
                posOS.x += sin(windPhase) * _WindStrength * windFactor;
                posOS.z += cos(windPhase * 0.7) * _WindStrength * 0.5 * windFactor;

                VertexPositionInputs posInputs = GetVertexPositionInputs(posOS);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // Grass blade shape: narrower at top, full at base
                float bladeShape = 1.0 - abs(input.uv.x - 0.5) * 2.0;
                float taper = saturate(bladeShape * (1.0 + input.uv.y * 0.5));

                // Alpha cutoff for blade shape
                clip(taper - _AlphaCutoff);

                // Color gradient: base color at bottom, tip color at top
                float3 albedo = lerp(_BaseColor.rgb, _TipColor.rgb, input.uv.y);

                // Simple banded lighting
                float3 normalWS = normalize(input.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                float NdotL = saturate(dot(normalWS, mainLight.direction));
                float banded = floor(NdotL * 2.0) / 2.0; // 2 bands for grass
                banded *= mainLight.shadowAttenuation;

                float3 litColor = albedo * mainLight.color;
                float3 shadowColor = albedo * _ShadowColor.rgb;
                float3 finalColor = lerp(shadowColor, litColor, banded);

                // Ambient
                finalColor += albedo * SampleSH(normalWS) * 0.3;

                // Fog
                finalColor = MixFog(finalColor, input.fogFactor);

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
