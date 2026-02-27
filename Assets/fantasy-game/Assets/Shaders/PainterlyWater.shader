Shader "FantasyGame/PainterlyWater"
{
    Properties
    {
        _BaseColor ("Shallow Color", Color) = (0.15, 0.35, 0.5, 0.65)
        _DeepColor ("Deep Color", Color) = (0.05, 0.15, 0.3, 0.8)
        _WaveSpeed ("Wave Speed", Float) = 0.8
        _WaveHeight ("Wave Height", Float) = 0.15
        _WaveFrequency ("Wave Frequency", Float) = 2.0
        _FresnelPower ("Fresnel Power", Float) = 3.0
        _SpecularColor ("Specular Color", Color) = (1, 0.95, 0.8, 1)
        _SpecularPower ("Specular Power", Float) = 32
        _FoamColor ("Foam Color", Color) = (0.8, 0.9, 1.0, 0.6)
        _WaveTime ("Wave Time", Float) = 0
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "WaterForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 viewDirWS : TEXCOORD3;
                float4 vertColor : COLOR;
                float fogFactor : TEXCOORD4;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _DeepColor;
                float _WaveSpeed;
                float _WaveHeight;
                float _WaveFrequency;
                float _FresnelPower;
                float4 _SpecularColor;
                float _SpecularPower;
                float4 _FoamColor;
                float _WaveTime;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float3 posOS = IN.positionOS.xyz;

                // Use _Time.y if _WaveTime is 0
                float t = _WaveTime > 0.001 ? _WaveTime : _Time.y;

                // Animated waves
                float wave1 = sin(posOS.x * _WaveFrequency + t * _WaveSpeed * 1.3) * _WaveHeight * 0.6;
                float wave2 = sin(posOS.z * _WaveFrequency * 0.7 + t * _WaveSpeed * 0.9) * _WaveHeight * 0.4;
                float wave3 = sin((posOS.x + posOS.z) * _WaveFrequency * 0.5 + t * _WaveSpeed * 1.7) * _WaveHeight * 0.3;
                posOS.y += wave1 + wave2 + wave3;

                VertexPositionInputs posInputs = GetVertexPositionInputs(posOS);
                OUT.positionCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;

                // Compute wave normal from derivatives
                float dx = cos(posOS.x * _WaveFrequency + t * _WaveSpeed * 1.3) * _WaveHeight * 0.6 * _WaveFrequency;
                float dz = cos(posOS.z * _WaveFrequency * 0.7 + t * _WaveSpeed * 0.9) * _WaveHeight * 0.4 * _WaveFrequency * 0.7;
                float3 waveNormal = normalize(float3(-dx, 1.0, -dz));
                OUT.normalWS = TransformObjectToWorldNormal(waveNormal);

                OUT.viewDirWS = GetWorldSpaceNormalizeViewDir(OUT.positionWS);
                OUT.uv = IN.uv;
                OUT.vertColor = IN.color;
                OUT.fogFactor = ComputeFogFactor(posInputs.positionCS.z);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(IN.viewDirWS);

                // Fresnel effect — more reflective at glancing angles
                float fresnel = pow(1.0 - saturate(dot(N, V)), _FresnelPower);

                // Mix shallow and deep colors based on vertex alpha (edge falloff)
                float edgeFactor = IN.vertColor.a;
                half4 waterColor = lerp(_DeepColor, _BaseColor, edgeFactor);

                // Add fresnel reflection (brightens edges)
                waterColor.rgb = lerp(waterColor.rgb, _SpecularColor.rgb * 0.5, fresnel * 0.4);

                // Main light specular
                Light mainLight = GetMainLight();
                float3 L = normalize(mainLight.direction);
                float3 H = normalize(L + V);
                float spec = pow(saturate(dot(N, H)), _SpecularPower);
                waterColor.rgb += _SpecularColor.rgb * spec * mainLight.color.rgb * 0.5;

                // Simple diffuse contribution from main light
                float NdotL = saturate(dot(N, L));
                waterColor.rgb += mainLight.color.rgb * NdotL * 0.1;

                // Foam at wave peaks
                float t = _WaveTime > 0.001 ? _WaveTime : _Time.y;
                float wavePeak = sin(IN.positionWS.x * _WaveFrequency + t * _WaveSpeed * 1.3);
                float foam = smoothstep(0.7, 1.0, wavePeak) * edgeFactor;
                waterColor.rgb = lerp(waterColor.rgb, _FoamColor.rgb, foam * 0.3);

                // Alpha: use vertex color alpha for edge falloff
                waterColor.a = edgeFactor * _BaseColor.a;
                waterColor.a = lerp(waterColor.a, waterColor.a + 0.2, fresnel);
                waterColor.a = saturate(waterColor.a);

                // Apply fog
                waterColor.rgb = MixFog(waterColor.rgb, IN.fogFactor);

                return waterColor;
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Unlit"
}
