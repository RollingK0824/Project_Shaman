Shader "ProjectShaman/Items/Salt Particles"
{
    Properties { _Dust ("Powder mask", Float) = 0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _Dust;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; half fog : TEXCOORD1; };
            Varyings Vert(Attributes v)
            {
                Varyings o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv; o.color = v.color; o.fog = ComputeFogFactor(o.positionCS.z); return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float2 p = i.uv * 2 - 1;
                float crystal = max(abs(p.x) * .88 + abs(p.y) * .3, abs(p.y) * .83 + abs(p.x) * .28);
                float grain = 1 - smoothstep(.66, .91, crystal);
                float radial = length(p);
                float cloud = pow(saturate(1 - radial), 1.7) * (.82 + .18 * sin(p.x * 13 + sin(p.y * 11)));
                float mask = lerp(grain, cloud, saturate(_Dust));
                float facet = lerp(.78 + .22 * step(p.x, p.y * .45), 1, saturate(_Dust));
                half4 c = half4(i.color.rgb * facet, i.color.a * mask);
                c.rgb = MixFog(c.rgb, i.fog);
                return c;
            }
            ENDHLSL
        }
    }
}
