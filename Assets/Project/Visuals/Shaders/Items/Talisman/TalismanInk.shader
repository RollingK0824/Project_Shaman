Shader "ProjectShaman/Talisman Ink"
{
    Properties
    {
        _BaseMap ("Original model texture", 2D) = "white" {}
        _DrawingInk ("Saved drawing ink", 2D) = "black" {}
        _ProjectionMin ("Mesh X/Z minimum", Vector) = (0,0,0,0)
        _ProjectionSize ("Mesh X/Z size", Vector) = (1,1,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_DrawingInk); SAMPLER(sampler_DrawingInk);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _ProjectionMin;
                float4 _ProjectionSize;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float2 inkUV : TEXCOORD1; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                float2 projected = (v.positionOS.xz - _ProjectionMin.xy) / max(_ProjectionSize.xy, float2(.0001,.0001));
                o.inkUV = float2(.17 + .66 * projected.x, projected.y);
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                half4 paper = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                half4 ink = SAMPLE_TEXTURE2D(_DrawingInk, sampler_DrawingInk, i.inkUV);
                return half4(lerp(paper.rgb, ink.rgb, ink.a), 1);
            }
            ENDHLSL
        }
    }
}
