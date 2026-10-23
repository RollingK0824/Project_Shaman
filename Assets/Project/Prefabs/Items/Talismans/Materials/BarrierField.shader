Shader "ProjectShaman/Items/BarrierField"
{
    Properties
    {
        [HDR] _BaseColor("Glow Color", Color) = (.52, .54, .56, 1)
        _Opacity("Opacity", Range(0,1)) = .7
        _Pulse("Activation Pulse", Range(0,1)) = 0
        [Toggle] _IsLine("Seal Line", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Opacity, _Pulse, _IsLine;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float3 positionOS:TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionOS = input.positionOS.xyz;
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float alpha = _Opacity;
                if (_IsLine < .5)
                {
                    float facing = abs(dot(normalize(input.normalWS), GetWorldSpaceNormalizeViewDir(input.positionWS)));
                    float rim = pow(saturate(1 - facing), 3);
                    float band = smoothstep(.97, 1, cos(input.positionOS.y * 38 - _Time.y * .65));
                    alpha *= .018 + rim * .48 + band * .10 + _Pulse * (.12 + rim * .3);
                }
                half3 color = lerp(_BaseColor.rgb, half3(.72, .74, .76), _Pulse * .35);
                return half4(color, saturate(alpha * _BaseColor.a));
            }
            ENDHLSL
        }
    }
}
