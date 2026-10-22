Shader "ProjectShaman/Stylized Water"
{
    Properties
    {
        _ShallowColor ("Shallow Water", Color) = (0.18, 0.72, 0.82, 0.78)
        _DeepColor ("Deep Water", Color) = (0.015, 0.20, 0.38, 0.92)
        _FoamColor ("Foam and Highlights", Color) = (0.72, 0.96, 1.0, 0.9)
        _WaveScale ("Wave Scale", Float) = 0.65
        _WaveSpeed ("Wave Speed", Float) = 0.75
        _WaveStrength ("Wave Strength", Range(0, 1)) = 0.22
        _FoamAmount ("Foam Amount", Range(0, 1)) = 0.94
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 3.0
        _Opacity ("Opacity", Range(0, 1)) = 0.88
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" }
        LOD 100

        Pass
        {
            Name "StylizedWater"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float4 _FoamColor;
                float _WaveScale;
                float _WaveSpeed;
                float _WaveStrength;
                float _FoamAmount;
                float _FresnelPower;
                float _Opacity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.positionWS.xz * max(_WaveScale, 0.001);
                float time = _Time.y * _WaveSpeed;

                float waveA = sin(dot(p, float2(1.0, 0.72)) + time);
                float waveB = sin(dot(p, float2(-0.58, 1.0)) - time * 1.31 + 1.7);
                float waveC = sin(dot(p, float2(1.45, -1.1)) + time * 0.68 + 0.8);
                float wave = (waveA + waveB * 0.58 + waveC * 0.28) / 1.86;
                float waveMask = saturate(wave * 0.5 + 0.5);

                float3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 normalWS = normalize(input.normalWS);
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDir)), _FresnelPower);

                // Broad, low-contrast wave variation avoids isolated bright spots.
                float depthBlend = saturate(0.28 + waveMask * 0.46 + wave * _WaveStrength * 0.18);
                float3 waterColor = lerp(_DeepColor.rgb, _ShallowColor.rgb, depthBlend);
                float softHighlight = smoothstep(0.38, 0.92, waveMask) * _WaveStrength * 0.22;
                float foam = smoothstep(_FoamAmount, min(_FoamAmount + 0.12, 1.0), waveMask) * 0.18;
                float edgeHighlight = saturate(fresnel * 0.42 + softHighlight + foam);
                waterColor = lerp(waterColor, _FoamColor.rgb, edgeHighlight);

                float alpha = saturate(_Opacity * lerp(_DeepColor.a, _ShallowColor.a, depthBlend) + fresnel * 0.08);
                return half4(waterColor, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}