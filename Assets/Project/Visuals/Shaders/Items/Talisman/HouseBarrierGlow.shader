Shader "ProjectShaman/Items/House Barrier Glow"
{
    Properties
    {
        _Color ("Spirit mist", Color) = (.68,.7,.68,.65)
        _BaseMap ("Talisman symbol", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            CBUFFER_END
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            Varyings Vert(Attributes v)
            {
                Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.uv=v.uv; return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                // Larger than the paper quad, with the same texture alignment.
                float2 uv=(i.uv-.5)*(.38/.28)+.5;
                float inside=step(0,uv.x)*step(uv.x,1)*step(0,uv.y)*step(uv.y,1);
                half4 paper=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv)*inside;
                float2 p=(i.uv-.5)*float2(.38,.38);
                float time=_Time.y;
                float pulse=.82+.18*sin(time*1.45);
                // Soft, uneven mist, never a rectangular outline or a row of beads.
                float sway=.006*sin(p.y*35-time*.9)+.003*sin(p.y*73+time*.6);
                float2 oval=float2((p.x+sway)/.085,p.y/.155);
                float haze=exp(-dot(oval,oval)*1.7);
                float ripple=.72+.28*sin(p.y*47-time*1.3+sin(p.x*85+time*.7));
                float edgeFade=1-smoothstep(.78,1,max(abs(i.uv.x*2-1),abs(i.uv.y*2-1)));
                float mist=haze*ripple*.34*(1-paper.a)*edgeFade;
                // Derive light from the existing red ink; paper texture stays readable.
                float ink=paper.a*(1-smoothstep(.22,.36,paper.g/max(paper.r,.001)));
                half3 light=_Color.rgb*mist + half3(1,.16,.035)*ink*.26;
                return half4(light*pulse,_Color.a);
            }
            ENDHLSL
        }
    }
}
