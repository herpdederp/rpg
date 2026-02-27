Shader "FantasyGame/PainterlySky"
{
    Properties
    {
        _ZenithColor ("Zenith Color", Color) = (0.25, 0.40, 0.70, 1)
        _HorizonColor ("Horizon Color", Color) = (0.85, 0.70, 0.50, 1)
        _NadirColor ("Nadir Color", Color) = (0.15, 0.18, 0.30, 1)
        _HorizonSharpness ("Horizon Sharpness", Range(1, 8)) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Background"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Background"
        }

        Cull Front  // Render inside of sphere
        ZWrite Off
        ZTest LEqual

        Pass
        {
            Name "SkyDome"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 viewDir : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _ZenithColor;
                float4 _HorizonColor;
                float4 _NadirColor;
                float _HorizonSharpness;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                // Use object space direction as view direction
                // (sphere is centered on camera, so object pos IS the view direction)
                output.viewDir = normalize(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.viewDir);

                // Y component: +1 = zenith, 0 = horizon, -1 = nadir
                float y = dir.y;

                // Upper hemisphere: horizon -> zenith
                float upperBlend = saturate(pow(saturate(y), 1.0 / _HorizonSharpness));
                float3 upperColor = lerp(_HorizonColor.rgb, _ZenithColor.rgb, upperBlend);

                // Lower hemisphere: horizon -> nadir
                float lowerBlend = saturate(pow(saturate(-y), 1.0 / _HorizonSharpness));
                float3 lowerColor = lerp(_HorizonColor.rgb, _NadirColor.rgb, lowerBlend);

                // Select based on which hemisphere
                float3 skyColor = y >= 0 ? upperColor : lowerColor;

                return half4(skyColor, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
